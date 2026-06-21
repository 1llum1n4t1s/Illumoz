using Pb = Mozc.Commands;

namespace Mozc.Renderer;

// 候補ウィンドウ下部の footer 情報(C++ src/renderer の footer 描画相当)。
// index 表示("3/12")・status ラベル・sub ラベル・logo 可視を算出する。
public readonly record struct FooterInfo(
    bool Visible,
    bool IndexVisible,
    string IndexLabel,
    string Label,
    string SubLabel,
    bool LogoVisible);

public static class CandidateWindowFooter
{
    // CandidateWindow から footer 情報を組み立てる。
    // index ラベルは focused_index(ページ内位置)を全体位置へ補正し "現在/全体" にする。
    public static FooterInfo Build(Pb.CandidateWindow? cw)
    {
        if (cw == null || cw.Footer == null)
        {
            return new FooterInfo(false, false, string.Empty, string.Empty, string.Empty, false);
        }

        Pb.Footer footer = cw.Footer;
        string indexLabel = string.Empty;
        if (footer.IndexVisible && cw.Size > 0 && cw.Candidate.Count > 0)
        {
            // focused_index はページ先頭からの相対位置。ページ先頭の絶対 index を足す。
            int pageHead = (int)cw.Candidate[0].Index;
            int current = pageHead + (int)cw.FocusedIndex + 1;
            indexLabel = $"{current}/{cw.Size}";
        }

        return new FooterInfo(
            Visible: true,
            IndexVisible: footer.IndexVisible,
            IndexLabel: indexLabel,
            Label: footer.Label ?? string.Empty,
            SubLabel: footer.SubLabel ?? string.Empty,
            LogoVisible: footer.LogoVisible);
    }
}
