namespace Mozc.Renderer;

// C++ src/renderer/table_layout.cc の忠実移植。候補ウィンドウの行列セル配置を計算する
// (描画なし・純ジオメトリ)。Initialize→EnsureCellSize 等で寸法を積み上げ、
// FreezeLayout で確定、各 GetXxxRect で矩形を得る。
public sealed class TableLayout
{
    private const int MinimumIndicatorHeight = 1;

    private int[] _columnWidth = global::System.Array.Empty<int>();
    private Size _totalSize;
    private Size _minimumFooterSize;
    private Size _minimumHeaderSize;
    private int _ensureWidthFromColumn;
    private int _ensureWidthToColumn;
    private int _ensureWidth;
    private int _numberOfRows = 1;
    private int _numberOfColumns = 1;
    private int _windowBorderPixels = 1;
    private int _rowRectPaddingPixels;
    private int _rowHeight = 1;
    private int _vscrollWidthPixels;
    private bool _frozen;

    public int NumberOfRows => _numberOfRows;
    public int NumberOfColumns => _numberOfColumns;
    public bool IsLayoutFrozen => _frozen;

    public void Initialize(int numRows, int numColumns)
    {
        _numberOfRows = numRows;
        _numberOfColumns = numColumns;
        _frozen = false;
        _windowBorderPixels = 0;
        _minimumFooterSize = new Size(0, 0);
        _minimumHeaderSize = new Size(0, 0);
        _rowRectPaddingPixels = 0;
        _rowHeight = 0;
        _vscrollWidthPixels = 0;
        _columnWidth = new int[numColumns];
        _totalSize = default;
    }

    public void SetVScrollBar(int widthInPixels)
    {
        if (!_frozen) _vscrollWidthPixels = widthInPixels;
    }

    public void SetWindowBorder(int widthInPixels)
    {
        if (!_frozen) _windowBorderPixels = widthInPixels;
    }

    public void SetRowRectPadding(int widthPixels)
    {
        if (!_frozen) _rowRectPaddingPixels = widthPixels;
    }

    public void EnsureCellSize(int column, Size size)
    {
        if (_frozen) return;
        _columnWidth[column] = global::System.Math.Max(_columnWidth[column], size.Width);
        _rowHeight = global::System.Math.Max(_rowHeight, size.Height + _rowRectPaddingPixels * 2);
    }

    public void EnsureColumnsWidth(int fromColumn, int toColumn, int width)
    {
        _ensureWidthFromColumn = fromColumn;
        _ensureWidthToColumn = toColumn;
        _ensureWidth = width;
    }

    public void EnsureFooterSize(Size size)
    {
        if (_frozen) return;
        _minimumFooterSize = new Size(
            global::System.Math.Max(_minimumFooterSize.Width, size.Width),
            global::System.Math.Max(_minimumFooterSize.Height, size.Height));
    }

    public void EnsureHeaderSize(Size size)
    {
        if (_frozen) return;
        _minimumHeaderSize = new Size(
            global::System.Math.Max(_minimumHeaderSize.Width, size.Width),
            global::System.Math.Max(_minimumHeaderSize.Height, size.Height));
    }

    public void FreezeLayout()
    {
        if (_frozen) return;

        if (_ensureWidthFromColumn >= 0 && _ensureWidthFromColumn < _numberOfColumns
            && _ensureWidthToColumn > _ensureWidthFromColumn && _ensureWidthToColumn < _numberOfColumns
            && _ensureWidth > 0)
        {
            int ranged = 0;
            for (int i = _ensureWidthFromColumn; i <= _ensureWidthToColumn; i++)
            {
                ranged += _columnWidth[i];
            }
            if (ranged < _ensureWidth)
            {
                _columnWidth[_ensureWidthToColumn] += _ensureWidth - ranged;
            }
        }

        int allCellWidth = Sum(_columnWidth, 0, _numberOfColumns);
        int tableWidth = _rowRectPaddingPixels * 2 + allCellWidth + _vscrollWidthPixels;
        int contentWidth = global::System.Math.Max(tableWidth,
            global::System.Math.Max(_minimumFooterSize.Width, _minimumHeaderSize.Width));
        int width = contentWidth + _windowBorderPixels * 2;

        int allCellHeight = _rowHeight * _numberOfRows;
        int height = _windowBorderPixels * 2 + _minimumHeaderSize.Height
            + allCellHeight + _minimumFooterSize.Height;

        _totalSize = new Size(width, height);
        _frozen = true;
    }

    public Rect GetCellRect(int row, int column)
    {
        if (!_frozen) return default;
        int widthOfLeftCells = Sum(_columnWidth, 0, column);
        int left = _windowBorderPixels + _rowRectPaddingPixels + widthOfLeftCells;
        int top = _windowBorderPixels + _minimumHeaderSize.Height + _rowHeight * row;
        var rect = new Rect(left, top, _columnWidth[column], _rowHeight);
        return rect.Deflate(0, _rowRectPaddingPixels, 0, _rowRectPaddingPixels);
    }

    public Size GetTotalSize() => _frozen ? _totalSize : default;

    public Rect GetHeaderRect()
    {
        if (!_frozen) return default;
        int width = _totalSize.Width - _windowBorderPixels * 2;
        return new Rect(_windowBorderPixels, _windowBorderPixels, width, _minimumHeaderSize.Height);
    }

    public Rect GetFooterRect()
    {
        if (!_frozen) return default;
        int width = _totalSize.Width - _windowBorderPixels * 2;
        int top = _totalSize.Height - _windowBorderPixels - _minimumFooterSize.Height;
        return new Rect(_windowBorderPixels, top, width, _minimumFooterSize.Height);
    }

    public Rect GetVScrollBarRect()
    {
        if (!_frozen) return default;
        int left = _totalSize.Width - _windowBorderPixels - _vscrollWidthPixels;
        int top = _windowBorderPixels + _minimumHeaderSize.Height;
        int height = _totalSize.Height - _windowBorderPixels * 2
            - _minimumHeaderSize.Height - _minimumFooterSize.Height;
        return new Rect(left, top, _vscrollWidthPixels, height);
    }

    public Rect GetVScrollIndicatorRect(int beginIndex, int endIndex, int candidatesTotal)
    {
        Rect vscroll = GetVScrollBarRect();
        float barHeight = (float)vscroll.Height / candidatesTotal;
        float top = vscroll.Top + barHeight * beginIndex;
        float bottom = vscroll.Top + barHeight * (endIndex + 1);
        int roundedTop = (int)(top + 0.5f);
        int roundedHeight = (int)(bottom - top + 0.5f);
        if (roundedHeight < MinimumIndicatorHeight)
        {
            roundedHeight = MinimumIndicatorHeight;
        }
        if (roundedTop + roundedHeight > vscroll.Bottom)
        {
            roundedTop = vscroll.Bottom - roundedHeight;
        }
        return new Rect(new Point(vscroll.Left, roundedTop), new Size(vscroll.Width, roundedHeight));
    }

    public Rect GetRowRect(int row)
    {
        if (!_frozen) return default;
        int top = _windowBorderPixels + _minimumHeaderSize.Height + _rowHeight * row;
        int width = _totalSize.Width - _windowBorderPixels * 2 - _vscrollWidthPixels;
        return new Rect(_windowBorderPixels, top, width, _rowHeight);
    }

    public Rect GetColumnRect(int column)
    {
        if (!_frozen) return default;
        int widthOfLeftCells = Sum(_columnWidth, 0, column);
        int left = _windowBorderPixels + _rowRectPaddingPixels + widthOfLeftCells;
        int top = _windowBorderPixels + _minimumHeaderSize.Height;
        int height = _rowHeight * _numberOfRows;
        return new Rect(left, top, _columnWidth[column], height);
    }

    private static int Sum(int[] values, int from, int toExclusive)
    {
        int s = 0;
        for (int i = from; i < toExclusive; i++)
        {
            s += values[i];
        }
        return s;
    }
}
