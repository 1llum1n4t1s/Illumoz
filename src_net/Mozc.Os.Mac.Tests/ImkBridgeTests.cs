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
        Assert.Contains("私", ImkBridge.CandidatesManaged.Split('\n')); // 候補列が native へ渡せる
        ImkBridge.ProcessKeyManaged(49, " ", 0); // Space(keyCode 49)
        var committed = ImkBridge.ProcessKeyManaged(36, "", 0); // Return
        Assert.Equal("私", committed.Commit);
    }

    [Fact]
    public void CommandShortcut_NotConsumed_PreservesPreedit()
    {
        // Cmd(⌘)修飾のキー(Cmd+C 等)は IME 対象外。サーバへ送らず未消費で素通しし、
        // 進行中の preedit(marked text)は保持する(C++ KeyCodeMap の return NO 相当)。
        const uint Command = 1u << 20; // NSEventModifierFlagCommand
        ImkBridge.InitForTest(Server().HandleProtoRequest);
        (string Preedit, string Commit, bool Consumed) last = default;
        foreach (char c in "watashi")
        {
            last = ImkBridge.ProcessKeyManaged(0, c.ToString(), 0);
        }
        Assert.Equal("わたし", last.Preedit);

        // Cmd+C(keyCode 8='c')は未消費で返り、合成中の preedit は消えない。
        var cmdC = ImkBridge.ProcessKeyManaged(8, "c", Command);
        Assert.False(cmdC.Consumed);
        Assert.Equal("わたし", cmdC.Preedit);
        Assert.Equal(string.Empty, cmdC.Commit);
    }
}
