using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Session;
using Xunit;

namespace Mozc.Session.Tests;

public class SessionHandlerTests
{
    private static List<string> PosRules()
    {
        var rules = new List<string> { "Functional ^助詞" };
        for (int i = 1; i < PosMatcher.RuleCount; i++)
        {
            rules.Add($"R{i} ^ZZ{i}");
        }
        return rules;
    }

    private const string RomanTable = "wa\tわ\nta\tた\nshi\tし";

    private static MozcEngine Engine()
    {
        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[] { "わたし\t1\t1\t100\t私" },
            ConnectionLines = new[] { "2", "0", "0", "0", "0" },
            ConnectionSpecialPosSize = 0,
            IdDefLines = new[] { "0 BOS/EOS,*,*,*,*,*,*", "1 名詞,一般,*,*,*,*,*" },
            SpecialPosLines = global::System.Array.Empty<string>(),
            PosMatcherRuleLines = PosRules(),
            SegmenterRuleLines = global::System.Array.Empty<string>(),
            BoundaryDefLines = global::System.Array.Empty<string>(),
        };
        return new MozcEngine(new DataSetBuilder().Build(sources), RomanTable);
    }

    private static KeyMap Keymap()
    {
        var km = new KeyMap();
        km.LoadFromString("Composition\tSpace\tConvertNext\nConversion\tEnter\tCommit");
        return km;
    }

    private static SessionHandler Handler() => new(Engine(), Keymap());

    [Fact]
    public void CreateSendDelete_Flow()
    {
        SessionHandler h = Handler();
        Output created = h.EvalCommand(new Input { Type = CommandType.CreateSession });
        Assert.False(created.ErrorOccured);
        ulong id = created.SessionId;
        Assert.Equal(1, h.SessionCount);

        foreach (char c in "watashi")
        {
            h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = id, KeyString = c.ToString() });
        }
        Output conv = h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = id, KeyString = "Space" });
        Assert.Equal("私", conv.Preedit);
        Assert.Contains("私", conv.Candidates);

        Output commit = h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = id, KeyString = "Enter" });
        Assert.Equal("私", commit.Result);

        Output del = h.EvalCommand(new Input { Type = CommandType.DeleteSession, SessionId = id });
        Assert.True(del.Consumed);
        Assert.Equal(0, h.SessionCount);
    }

    [Fact]
    public void SendToUnknownSession_Errors()
    {
        SessionHandler h = Handler();
        Output o = h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = 999, KeyString = "a" });
        Assert.True(o.ErrorOccured);
    }

    [Fact]
    public void MultipleSessions_AreIndependent()
    {
        SessionHandler h = Handler();
        ulong a = h.EvalCommand(new Input { Type = CommandType.CreateSession }).SessionId;
        ulong b = h.EvalCommand(new Input { Type = CommandType.CreateSession }).SessionId;
        Assert.NotEqual(a, b);

        h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = a, KeyString = "w" });
        h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = a, KeyString = "a" });
        Output ob = h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = b, KeyString = "t" });
        // セッション b は a の影響を受けない(t→pending, わ ではない)。
        Assert.DoesNotContain("わ", ob.Preedit);
    }

    [Fact]
    public void CreateSession_OverLimit_EvictsLruInsteadOfError()
    {
        SessionHandler h = Handler();
        var ids = new List<ulong>();
        for (int i = 0; i < SessionHandler.MaxSessions; i++)
        {
            Output c = h.EvalCommand(new Input { Type = CommandType.CreateSession });
            Assert.False(c.ErrorOccured);
            ids.Add(c.SessionId);
        }
        Assert.Equal(SessionHandler.MaxSessions, h.SessionCount);

        // 最古(ids[0])以外を一度使って LRU を更新する → 破棄対象は ids[0] のまま。
        for (int i = 1; i < ids.Count; i++)
        {
            h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = ids[i], KeyString = "a" });
        }

        // 上限到達後の新規作成はエラーにならず成功し、件数は上限を超えない。
        Output extra = h.EvalCommand(new Input { Type = CommandType.CreateSession });
        Assert.False(extra.ErrorOccured);
        Assert.Equal(SessionHandler.MaxSessions, h.SessionCount);

        // 最も長く使われていない ids[0] が破棄されている(送信するとエラー)。
        Output dead = h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = ids[0], KeyString = "a" });
        Assert.True(dead.ErrorOccured);
        // 直近使用の ids[1] は生存している。
        Output alive = h.EvalCommand(new Input { Type = CommandType.SendKey, SessionId = ids[1], KeyString = "a" });
        Assert.False(alive.ErrorOccured);
    }
}
