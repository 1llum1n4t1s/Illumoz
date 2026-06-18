using Mozc.Renderer;
using Xunit;

namespace Mozc.Renderer.Tests;

public class CandidateWindowLayouterTests
{
    // 等幅: 1 文字 8px 幅 / 16px 高 とみなす簡易計測。
    private static Size Measure(string s) => new(s.Length * 8, 16);

    [Fact]
    public void Build_RowsAndColumnsSized()
    {
        var candidates = new List<CandidateRow>
        {
            new("1", "私", "名詞"),
            new("2", "渡し", "名詞"),
            new("3", "わたし", ""),
        };
        TableLayout layout = CandidateWindowLayouter.Build(candidates, Measure,
            gapWidth: 4, rowPadding: 2, windowBorder: 1, vscrollWidth: 0);

        Assert.True(layout.IsLayoutFrozen);
        Assert.Equal(3, layout.NumberOfRows);
        Assert.Equal(4, layout.NumberOfColumns);

        // 候補列の幅 = 最長候補(わたし=3文字=24px)。
        Assert.Equal(24, layout.GetColumnRect(CandidateWindowLayouter.ColumnValue).Width);
        // 行高 = 文字高(16) + rowPadding*2(4) = 20。
        Assert.Equal(20, layout.GetRowRect(0).Height);
        // 総幅 > 0、行数ぶんの高さ。
        Assert.True(layout.GetTotalSize().Width > 0);
        Assert.True(layout.GetTotalSize().Height >= 20 * 3);
    }

    [Fact]
    public void Build_EmptyCandidates_StillFreezes()
    {
        TableLayout layout = CandidateWindowLayouter.Build(
            global::System.Array.Empty<CandidateRow>(), Measure);
        Assert.True(layout.IsLayoutFrozen);
        Assert.Equal(1, layout.NumberOfRows);
    }
}
