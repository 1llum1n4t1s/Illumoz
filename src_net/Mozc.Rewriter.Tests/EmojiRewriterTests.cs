using Mozc.Converter;
using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

public class EmojiRewriterTests
{
    private static string DataPath(string rel)
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        while (dir != null)
        {
            string c = global::System.IO.Path.Combine(dir.FullName, "src", "data", rel);
            if (global::System.IO.File.Exists(c)) { return c; }
            dir = dir.Parent;
        }
        return string.Empty;
    }

    private static Segments OneSegment(string key, string value)
    {
        var segments = new Segments();
        Segment seg = segments.AddSegment();
        seg.SetKey(key);
        Candidate c = seg.AddCandidate();
        c.Key = key; c.Value = value; c.ContentKey = key; c.ContentValue = value;
        return segments;
    }

    [Fact]
    public void LoadTable_ParsesRealEmojiData()
    {
        string path = DataPath(global::System.IO.Path.Combine("emoji", "emoji_data.tsv"));
        Assert.True(global::System.IO.File.Exists(path), $"emoji_data.tsv not found: {path}");
        var table = EmojiRewriter.LoadTable(global::System.IO.File.ReadAllText(path));
        Assert.NotEmpty(table);
        Assert.True(table.ContainsKey("えがお"));
        Assert.Contains("😀", table["えがお"]);
    }

    [Fact]
    public void Rewrite_AppendsEmoji()
    {
        var table = new Dictionary<string, string[]> { ["ねこ"] = new[] { "🐈️", "🐱" } };
        var rewriter = new EmojiRewriter(table);
        Segments segments = OneSegment("ねこ", "猫");

        Assert.True(rewriter.Rewrite(segments));
        Segment seg = segments.ConversionSegment(0);
        var values = new List<string>();
        for (int i = 0; i < seg.CandidatesSize; i++)
        {
            values.Add(seg.Get(i).Value);
        }
        Assert.Contains("🐱", values);
    }

    [Fact]
    public void Rewrite_UnknownKey_NoOp()
    {
        var rewriter = new EmojiRewriter(new Dictionary<string, string[]>());
        Assert.False(rewriter.Rewrite(OneSegment("あ", "亜")));
    }

    [Fact]
    public void Rewrite_Disabled_NoOp()
    {
        // config.use_emoji_conversion=false 相当: Enabled=false なら絵文字候補を出さない。
        var table = new Dictionary<string, string[]> { ["ねこ"] = new[] { "🐈️", "🐱" } };
        var rewriter = new EmojiRewriter(table) { Enabled = false };
        Segments segments = OneSegment("ねこ", "猫");
        Assert.False(rewriter.Rewrite(segments));
        Assert.Equal(1, segments.ConversionSegment(0).CandidatesSize);
    }
}
