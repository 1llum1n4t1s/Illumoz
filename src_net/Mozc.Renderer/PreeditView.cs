using Pb = Mozc.Commands;

namespace Mozc.Renderer;

// preedit(未確定文字列)の表示スタイル。C++/各 OS の composition 装飾相当。
public enum PreeditStyle
{
    Normal,     // NONE
    Underline,  // UNDERLINE: 変換前の下線
    Highlight,  // HIGHLIGHT: 注目文節の反転
}

// preedit セグメント1つ分(表示値とスタイル)。
public readonly record struct PreeditSegment(string Value, PreeditStyle Style);

// preedit 全体の表示情報。
public readonly record struct PreeditView(
    IReadOnlyList<PreeditSegment> Segments,
    string Text,
    int Cursor,
    int HighlightedSegmentIndex); // HIGHLIGHT 文節の index。無ければ -1。

// commands.Preedit → 表示用 PreeditView へ変換する(OS 統合層が composition 装飾に使う)。
public static class PreeditViewBuilder
{
    public static PreeditView Build(Pb.Preedit? preedit)
    {
        if (preedit == null || preedit.Segment.Count == 0)
        {
            return new PreeditView(global::System.Array.Empty<PreeditSegment>(), string.Empty, 0, -1);
        }

        var segs = new List<PreeditSegment>(preedit.Segment.Count);
        var sb = new global::System.Text.StringBuilder();
        int highlightIndex = -1;
        for (int i = 0; i < preedit.Segment.Count; i++)
        {
            Pb.Preedit.Types.Segment s = preedit.Segment[i];
            PreeditStyle style = s.Annotation switch
            {
                Pb.Preedit.Types.Segment.Types.Annotation.Highlight => PreeditStyle.Highlight,
                Pb.Preedit.Types.Segment.Types.Annotation.Underline => PreeditStyle.Underline,
                _ => PreeditStyle.Normal,
            };
            if (style == PreeditStyle.Highlight && highlightIndex < 0)
            {
                highlightIndex = i;
            }
            segs.Add(new PreeditSegment(s.Value, style));
            sb.Append(s.Value);
        }

        return new PreeditView(segs, sb.ToString(), (int)preedit.Cursor, highlightIndex);
    }
}
