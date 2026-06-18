using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Os.Linux;
using Mozc.Server;
using Mozc.Session;
using Xunit;

namespace Mozc.Os.Linux.Tests;

public class IbusBridgeTests
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
        IbusBridge.InitForTest(Server().HandleProtoRequest);
        // w a t a s h i を keysym(Latin-1)で送る。
        (string Preedit, string Commit, bool Consumed) last = default;
        foreach (char c in "watashi")
        {
            last = IbusBridge.ProcessKeyManaged(c, 0);
        }
        Assert.Equal("わたし", last.Preedit);
        // 入力中サジェストが候補列(改行区切り)として native へ渡せる。
        Assert.Contains("私", IbusBridge.CandidatesManaged.Split('\n'));

        // Space(keysym 0x20)で変換 → Enter(0xff0d)で確定。
        IbusBridge.ProcessKeyManaged(0x20, 0);
        var committed = IbusBridge.ProcessKeyManaged(0xff0d, 0);
        Assert.Equal("私", committed.Commit);
    }

    [Fact]
    public unsafe void WriteUtf8_BufferContract()
    {
        // cap 不足 → 必要長を返し書かない。
        int needed = IbusBridge.WriteUtf8("私", null, 0);
        Assert.Equal(3, needed); // UTF-8 で 3 bytes
        Span<byte> buf = stackalloc byte[8];
        fixed (byte* p = buf)
        {
            int written = IbusBridge.WriteUtf8("私", p, buf.Length);
            Assert.Equal(3, written);
        }
    }
}
