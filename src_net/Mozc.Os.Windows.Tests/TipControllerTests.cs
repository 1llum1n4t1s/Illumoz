using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Os.Windows;
using Mozc.Server;
using Mozc.Session;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Os.Windows.Tests;

// TSF TIP 本体ロジック(TipController)を in-proc EngineServer(protobuf 経路)で検証。
// 実 COM 登録/IME 動作は実機 Windows でのみ確認可。
public class TipControllerTests
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
    public void TextService_ActivateDeactivate_DrivesController()
    {
        EngineServer server = Server();
        var svc = new MozcTextService { Transport = server.HandleProtoRequest };

        Assert.Equal(0, svc.ActivateEx(threadMgr: 0, clientId: 1, flags: 0));
        Assert.True(svc.OnTestKeyDown('w')); // セッションが張られキーが消費される
        Assert.Equal(0, svc.Deactivate());
    }

    [Fact]
    public void FullFlow_ThroughTipController()
    {
        EngineServer server = Server();
        var tip = new TipController(server.HandleProtoRequest);

        foreach (char c in "watashi")
        {
            Assert.True(tip.OnCharacter(c));
        }
        Assert.Equal("わたし", tip.Preedit);

        tip.OnSpecialKey(Pb.KeyEvent.Types.SpecialKey.Space);
        Assert.Equal("私", tip.Preedit);
        Assert.Contains("私", tip.Candidates);

        tip.OnSpecialKey(Pb.KeyEvent.Types.SpecialKey.Enter);
        Assert.Equal("私", tip.LastCommit);

        tip.Shutdown();
    }
}
