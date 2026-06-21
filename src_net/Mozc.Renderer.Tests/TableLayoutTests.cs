using Mozc.Renderer;
using Xunit;

namespace Mozc.Renderer.Tests;

// C++ src/renderer/table_layout_test.cc のゴールデンケースを移植。
public class TableLayoutTests
{
    private const int ColumnShortcut = 0;
    private const int ColumnGap1 = 1;
    private const int ColumnCandidate = 2;
    private const int ColumnDescription = 3;
    private const int NumberOfColumns = 4;

    private static void AssertRect(Rect r, int left, int top, int width, int height)
    {
        Assert.Equal(left, r.Left);
        Assert.Equal(top, r.Top);
        Assert.Equal(width, r.Width);
        Assert.Equal(height, r.Height);
    }

    private static void AssertSize(Size s, int width, int height)
    {
        Assert.Equal(width, s.Width);
        Assert.Equal(height, s.Height);
    }

    [Fact]
    public void AllElement()
    {
        const int numberOfRow = 10;
        var layout = new TableLayout();
        layout.Initialize(numberOfRow, NumberOfColumns);
        layout.SetVScrollBar(11);
        layout.SetRowRectPadding(2);
        layout.SetWindowBorder(1);

        layout.EnsureHeaderSize(new Size(0, 9));
        layout.EnsureFooterSize(new Size(0, 13));
        layout.EnsureCellSize(ColumnGap1, new Size(5, 0));
        for (int row = 0; row < numberOfRow; row++)
        {
            layout.EnsureCellSize(ColumnCandidate, new Size(row + 1, 10));
            layout.EnsureCellSize(ColumnDescription, new Size(15, 5));
        }
        layout.FreezeLayout();

        AssertSize(layout.GetTotalSize(), 47, 164);
        AssertRect(layout.GetHeaderRect(), 1, 1, 45, 9);
        AssertRect(layout.GetFooterRect(), 1, 150, 45, 13);
        AssertRect(layout.GetVScrollBarRect(), 35, 10, 11, 140);
        AssertRect(layout.GetRowRect(1), 1, 24, 34, 14);
        AssertRect(layout.GetColumnRect(ColumnCandidate), 8, 10, 10, 140);
        AssertRect(layout.GetCellRect(1, ColumnShortcut), 3, 26, 0, 10);
        AssertRect(layout.GetCellRect(1, ColumnCandidate), 8, 26, 10, 10);
        AssertRect(layout.GetCellRect(1, ColumnDescription), 18, 26, 15, 10);
    }

    [Fact]
    public void AllElementWithMinimumFooterWidth()
    {
        const int numberOfRow = 10;
        var layout = new TableLayout();
        layout.Initialize(numberOfRow, NumberOfColumns);
        layout.SetVScrollBar(11);
        layout.SetRowRectPadding(2);
        layout.SetWindowBorder(1);
        layout.EnsureHeaderSize(new Size(0, 9));
        layout.EnsureFooterSize(new Size(100, 13));
        layout.EnsureCellSize(ColumnGap1, new Size(5, 0));
        for (int row = 0; row < numberOfRow; row++)
        {
            layout.EnsureCellSize(ColumnCandidate, new Size(row + 1, 10));
            layout.EnsureCellSize(ColumnDescription, new Size(15, 5));
        }
        layout.FreezeLayout();

        AssertSize(layout.GetTotalSize(), 102, 164);
        AssertRect(layout.GetFooterRect(), 1, 150, 100, 13);
        AssertRect(layout.GetVScrollBarRect(), 90, 10, 11, 140);
    }

    [Fact]
    public void EnsureCellsWidth()
    {
        var layout = new TableLayout();
        layout.Initialize(1, 4);
        for (int i = 0; i < 4; i++)
        {
            layout.EnsureCellSize(i, new Size(10, 10));
        }
        layout.EnsureColumnsWidth(1, 2, 100);
        layout.FreezeLayout();

        AssertSize(layout.GetTotalSize(), 120, 10);
        AssertRect(layout.GetColumnRect(0), 0, 0, 10, 10);
        AssertRect(layout.GetColumnRect(1), 10, 0, 10, 10);
        AssertRect(layout.GetColumnRect(2), 20, 0, 90, 10);
        AssertRect(layout.GetColumnRect(3), 110, 0, 10, 10);
    }
}
