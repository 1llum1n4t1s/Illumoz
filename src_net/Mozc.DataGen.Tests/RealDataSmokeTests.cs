using Mozc.Converter;
using Mozc.Engine;
using Xunit;

namespace Mozc.DataGen.Tests;

// 実 src/data 全量から生成した mozc.data の読込・変換スモーク。
// 生成物(環境依存・大)が存在する場合のみ実行(CI では通常 skip)。
// 生成: Mozc.DataGen --out <path> --dict ... --connection ... など(GenerateMozcData.targets 参照)。
public class RealDataSmokeTests
{
    private static string? RealDataPath()
    {
        string p = global::System.IO.Path.Combine(
            global::System.IO.Path.GetTempPath(), "mozc_real.data");
        return global::System.IO.File.Exists(p) ? p : null;
    }

    private static string? RomanTablePath()
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        string rel = "src/data/preedit/romanji-hiragana.tsv";
        while (dir != null && !global::System.IO.File.Exists(global::System.IO.Path.Combine(dir.FullName, rel)))
        {
            dir = dir.Parent;
        }
        return dir == null ? null : global::System.IO.Path.Combine(dir.FullName, rel);
    }

    [Fact]
    public void RealMozcData_LoadsAndConverts()
    {
        string? dataPath = RealDataPath();
        string? romanPath = RomanTablePath();
        if (dataPath == null || romanPath == null)
        {
            return; // 生成物が無ければ skip
        }

        byte[] data = global::System.IO.File.ReadAllBytes(dataPath);
        var engine = new MozcEngine(data, global::System.IO.File.ReadAllText(romanPath));

        var composer = engine.CreateComposer();
        composer.InsertCharacters("わたし"); // 直接かな投入も可
        // 実辞書で「わたし」の変換候補が得られること(値は環境の辞書に依存)。
        Segments segs = engine.Convert("わたし");
        Assert.True(segs.ConversionSegmentsSize >= 1);
        Assert.True(segs.ConversionSegment(0).CandidatesSize >= 1);
    }
}
