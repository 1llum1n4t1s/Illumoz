using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/emoji_rewriter.cc の中核スライス。読みに対応する絵文字を候補へ挿入する。
// 本家は emoji_data(data)由来。ここでは (読み->絵文字列) 辞書を注入可能にし、
// emoji_data.tsv をパースするローダを用意する。
public sealed class EmojiRewriter : IRewriter
{
    private readonly IReadOnlyDictionary<string, string[]> _table;

    public EmojiRewriter(IReadOnlyDictionary<string, string[]> table) => _table = table;

    // config.use_emoji_conversion=false で絵文字候補を出さない(C++ use_emoji_conversion() 相当)。
    // proto 既定は false のため、EngineServer.ApplyConfig が construct 直後に設定で上書きする。
    public bool Enabled { get; set; } = true;

    // emoji_data.tsv を (読み->絵文字列) へ(共通パーサに委譲)。
    public static IReadOnlyDictionary<string, string[]> LoadTable(string tsv)
        => Base.SymbolDataParser.ParseEmoji(tsv);

    public bool Rewrite(Segments segments)
    {
        if (!Enabled)
        {
            return false;
        }
        bool modified = false;
        for (int i = 0; i < segments.ConversionSegmentsSize; i++)
        {
            modified |= RewriteSegment(segments.ConversionSegment(i));
        }
        return modified;
    }

    private bool RewriteSegment(Segment segment)
    {
        if (segment.CandidatesSize == 0 || !_table.TryGetValue(segment.Key, out string[]? emojis))
        {
            return false;
        }

        var existing = new HashSet<string>();
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            existing.Add(segment.Get(i).Value);
        }

        Candidate baseCand = segment.Get(0);
        var newCands = new List<Candidate>();
        foreach (string e in emojis)
        {
            if (existing.Add(e))
            {
                newCands.Add(new Candidate
                {
                    Key = baseCand.Key,
                    Value = e,
                    ContentKey = baseCand.ContentKey,
                    ContentValue = e,
                    Description = "絵文字",
                    Cost = baseCand.Cost,
                });
            }
        }
        if (newCands.Count == 0)
        {
            return false;
        }
        // 絵文字は候補末尾に出す。
        segment.InsertCandidates(segment.CandidatesSize, newCands);
        return true;
    }
}
