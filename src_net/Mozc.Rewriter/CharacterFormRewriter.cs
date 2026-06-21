using Mozc.Base;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ で言う character_form_manager の変換適用部。各変換候補の value を
// conversion 用文字形マネージャで全角/半角整形する(数字・英字・カタカナの好み反映)。
// マネージャは config から再構築されるため差し替え可能にしておく。
public sealed class CharacterFormRewriter : IRewriter
{
    private CharacterFormManager _manager;

    public CharacterFormRewriter(CharacterFormManager? manager = null)
        => _manager = manager ?? CharacterFormManager.CreatePreeditDefault();

    // config 変更時に EngineServer から差し替える。
    public void SetManager(CharacterFormManager manager) => _manager = manager;

    public bool Rewrite(Segments segments)
    {
        bool modified = false;
        for (int si = 0; si < segments.ConversionSegmentsSize; si++)
        {
            Segment seg = segments.ConversionSegment(si);
            for (int i = 0; i < seg.CandidatesSize; i++)
            {
                Candidate c = seg.Get(i);
                string converted = _manager.ConvertString(c.Value);
                if (converted != c.Value)
                {
                    c.Value = converted;
                    c.ContentValue = _manager.ConvertString(c.ContentValue);
                    modified = true;
                }
            }
        }
        return modified;
    }
}
