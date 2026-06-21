namespace Mozc.Renderer;

// 候補 1 行分の表示データ(ショートカット/候補値/注釈)。
public readonly record struct CandidateRow(string Shortcut, string Value, string Description);

// C++ src/renderer の候補ウィンドウ描画前のレイアウト構築相当(純ロジック)。
// 候補リスト + テキスト計測関数から TableLayout を組んで凍結する。
// 列: 0=ショートカット, 1=ギャップ, 2=候補値, 3=注釈。描画は GUI 層(Avalonia)が担う。
public static class CandidateWindowLayouter
{
    public const int ColumnShortcut = 0;
    public const int ColumnGap = 1;
    public const int ColumnValue = 2;
    public const int ColumnDescription = 3;
    private const int NumColumns = 4;

    // measure: 文字列 → 表示サイズ(px)。GUI 層のフォント計測を注入する(テストでは固定計測)。
    public static TableLayout Build(
        IReadOnlyList<CandidateRow> candidates,
        Func<string, Size> measure,
        int gapWidth = 4,
        int rowPadding = 2,
        int windowBorder = 1,
        int vscrollWidth = 0)
    {
        var layout = new TableLayout();
        int rows = global::System.Math.Max(1, candidates.Count);
        layout.Initialize(rows, NumColumns);
        layout.SetWindowBorder(windowBorder);
        layout.SetRowRectPadding(rowPadding);
        layout.SetVScrollBar(vscrollWidth);

        layout.EnsureCellSize(ColumnGap, new Size(gapWidth, 0));
        foreach (CandidateRow row in candidates)
        {
            if (row.Shortcut.Length != 0)
            {
                layout.EnsureCellSize(ColumnShortcut, measure(row.Shortcut));
            }
            layout.EnsureCellSize(ColumnValue, measure(row.Value));
            if (row.Description.Length != 0)
            {
                layout.EnsureCellSize(ColumnDescription, measure(row.Description));
            }
        }
        layout.FreezeLayout();
        return layout;
    }
}
