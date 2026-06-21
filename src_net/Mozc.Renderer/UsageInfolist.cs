using Pb = Mozc.Commands;

namespace Mozc.Renderer;

// 用例(usage)インフォリスト1件分。C++ renderer の infolist 描画で使う。
public readonly record struct UsageInfo(int Id, string Title, string Description);

// 候補にフォーカスが当たったとき、対応する用例情報(usages)を選ぶ。
// C++ src/renderer の infolist 選択(focused 候補 id → Information.candidate_id 照合)相当。
public static class UsageInfolist
{
    // cw.Usages の中から、focused 候補の id に紐づく Information を返す。
    // 紐づくものが無ければ null(=infolist 非表示)。
    public static UsageInfo? ForFocusedCandidate(Pb.CandidateWindow? cw)
    {
        if (cw == null || cw.Usages == null || cw.Usages.Information.Count == 0
            || cw.Candidate.Count == 0)
        {
            return null;
        }

        // focused_index はページ内相対位置。範囲外なら非表示。
        int idx = (int)cw.FocusedIndex;
        if (idx < 0 || idx >= cw.Candidate.Count)
        {
            return null;
        }

        Pb.CandidateWindow.Types.Candidate focused = cw.Candidate[idx];
        if (!focused.HasId)
        {
            return null;
        }

        int focusedId = focused.Id;
        foreach (Pb.Information info in cw.Usages.Information)
        {
            foreach (int candId in info.CandidateId)
            {
                if (candId == focusedId)
                {
                    return new UsageInfo(info.Id, info.Title ?? string.Empty, info.Description ?? string.Empty);
                }
            }
        }
        return null;
    }
}
