using Mozc.Client;
using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Server;
using Mozc.Session;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Client.Tests;

public class ImeClientTests
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

    private static EngineServer Server()
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
        var engine = new MozcEngine(new DataSetBuilder().Build(sources), "wa\tわ\nta\tた\nshi\tし");
        var km = new KeyMap();
        km.LoadFromString("Composition\tSpace\tConvertNext\nConversion\tEnter\tCommit");
        return new EngineServer(engine, km);
    }

    [Fact]
    public void SendKey_Flow()
    {
        var client = new ImeClient(Server().HandleProtoRequest);
        ImeState s = default!;
        foreach (char c in "watashi")
        {
            s = client.SendCharacter(c);
        }
        Assert.Equal("わたし", s.Preedit);

        // 入力中は SUGGESTION 候補窓 + ショートカット。
        Assert.True(s.IsSuggestion);
        Assert.Contains("私", s.Candidates);
        Assert.Equal("1", s.Shortcuts[0]);

        s = client.SendSpecialKey(Pb.KeyEvent.Types.SpecialKey.Space);
        Assert.Contains("私", s.Candidates);
        Assert.False(s.IsSuggestion); // 変換後は通常候補

        s = client.SendSpecialKey(Pb.KeyEvent.Types.SpecialKey.Enter);
        Assert.Equal("私", s.Commit);
    }

    [Fact]
    public void SubmitCandidate_Commits()
    {
        var client = new ImeClient(Server().HandleProtoRequest);
        foreach (char c in "watashi")
        {
            client.SendCharacter(c);
        }
        client.SendSpecialKey(Pb.KeyEvent.Types.SpecialKey.Space);
        ImeState s = client.SubmitCandidate(0);
        Assert.Equal("私", s.Commit);
    }

    [Fact]
    public void SubmitByShortcut_Commits()
    {
        var client = new ImeClient(Server().HandleProtoRequest);
        ImeState s = default!;
        foreach (char c in "watashi")
        {
            s = client.SendCharacter(c);
        }
        // 入力中サジェストに shortcut "1"。'1' で直接確定(変換不要)。
        Assert.True(s.IsSuggestion);
        Assert.Equal("1", s.Shortcuts[0]);
        ImeState committed = client.SubmitByShortcut('1');
        Assert.Equal("私", committed.Commit);
    }
}
