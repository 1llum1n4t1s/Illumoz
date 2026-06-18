using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Os.Mac;
using Mozc.Server;
using Mozc.Session;
using Xunit;

namespace Mozc.Os.Mac.Tests;

public class ImkBridgeTests
{
    private static List<string> PosRules()
    {
        var rules = new List<string> { "Functional ^助詞" };
        for (int i = 1; i < PosMatcher.RuleCount; i++) rules.Add($"R{i} ^ZZ{i}");
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
    public void ProcessKey_ThroughBridge()
    {
        ImkBridge.InitForTest(Server().HandleProtoRequest);
        (string Preedit, string Commit, bool Consumed) last = default;
        foreach (char c in "watashi")
        {
            last = ImkBridge.ProcessKeyManaged(0, c.ToString(), 0);
        }
        Assert.Equal("わたし", last.Preedit);
        ImkBridge.ProcessKeyManaged(49, " ", 0); // Space(keyCode 49)
        var committed = ImkBridge.ProcessKeyManaged(36, "", 0); // Return
        Assert.Equal("私", committed.Commit);
    }
}
