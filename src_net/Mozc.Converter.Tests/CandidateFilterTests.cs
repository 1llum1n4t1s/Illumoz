using Mozc.Converter;
using Mozc.Dictionary;
using Xunit;

namespace Mozc.Converter.Tests;

public class CandidateFilterTests
{
    // 全規則が空レンジ(IsXxx は常に false, GetXxxId は 0)の PosMatcher。
    private static PosMatcher EmptyPosMatcher()
    {
        int n = PosMatcher.RuleCount; // 35
        var data = new ushort[n + n + 1];
        int sentinel = n + n; // 70
        data[sentinel] = 0xFFFF;
        for (int i = 0; i < n; i++) data[n + i] = (ushort)sentinel; // 全規則→空レンジ
        return new PosMatcher(data, n);
    }

    private static Node N(string key, string value, ushort lid, ushort rid)
        => new() { Key = key, Value = value, Lid = lid, Rid = rid };

    private static Candidate C(string key, string value, int cost, int structureCost, ushort lid, ushort rid)
        => new()
        {
            Key = key, Value = value, ContentKey = key, ContentValue = value,
            Cost = cost, StructureCost = structureCost, Lid = lid, Rid = rid,
        };

    private static CandidateFilter.Options Conv() => new() { RequestType = CandidateFilter.RequestType.Conversion };

    [Fact]
    public void SingleTokenCandidate_IsGood_AndDedups()
    {
        var f = new CandidateFilter(EmptyPosMatcher());
        var nodes = new List<Node> { N("あめ", "雨", 5, 5) };
        var cand = C("あめ", "雨", 100, 0, 5, 5);

        Assert.Equal(CandidateFilter.ResultType.GoodCandidate,
            f.FilterCandidate(Conv(), "あめ", cand, nodes, nodes));
        // 同一 (value,lid,rid) は重複として除外。
        Assert.Equal(CandidateFilter.ResultType.BadCandidate,
            f.FilterCandidate(Conv(), "あめ", C("あめ", "雨", 200, 0, 5, 5), nodes, nodes));
    }

    [Fact]
    public void Suggestion_BadSuggestion_IsFiltered()
    {
        var f = new CandidateFilter(EmptyPosMatcher(), isBadSuggestion: v => v == "雨");
        var nodes = new List<Node> { N("あめ", "雨", 5, 5) };
        var opts = new CandidateFilter.Options { RequestType = CandidateFilter.RequestType.Suggestion };
        Assert.Equal(CandidateFilter.ResultType.BadCandidate,
            f.FilterCandidate(opts, "あ", C("あめ", "雨", 100, 0, 5, 5), nodes, nodes));
    }

    [Fact]
    public void ReverseConversion_OnlyDedups()
    {
        var f = new CandidateFilter(EmptyPosMatcher());
        var nodes = new List<Node> { N("あめ", "雨", 5, 5) };
        var opts = new CandidateFilter.Options { RequestType = CandidateFilter.RequestType.ReverseConversion };
        Assert.Equal(CandidateFilter.ResultType.GoodCandidate,
            f.FilterCandidate(opts, "あめ", C("あめ", "雨", 100, 0, 5, 5), nodes, nodes));
        Assert.Equal(CandidateFilter.ResultType.BadCandidate,
            f.FilterCandidate(opts, "あめ", C("あめ", "雨", 100, 0, 5, 5), nodes, nodes));
    }

    [Fact]
    public void HighCostCandidate_IsFiltered()
    {
        var f = new CandidateFilter(EmptyPosMatcher());
        // top(低コスト, structure!=0, content!=value 回避のため content=value)。
        var topNodes = new List<Node> { N("あめ", "雨", 10, 10) };
        var top = C("あめ", "雨", 100, 50, 10, 10);
        Assert.Equal(CandidateFilter.ResultType.GoodCandidate,
            f.FilterCandidate(Conv(), "あめ", top, topNodes, topNodes));

        // 高コスト候補(2ノード, 値2文字)。cost/structure とも閾値超過 → Bad。
        var nodes = new List<Node> { N("あく", "悪", 11, 11), N("む", "夢", 11, 11) };
        var bad = C("あくむ", "悪夢", 100 + 6907 + 1, 100 + 1151 + 100, 11, 11);
        Assert.Equal(CandidateFilter.ResultType.BadCandidate,
            f.FilterCandidate(Conv(), "あくむ", bad, topNodes, nodes));
    }

    [Fact]
    public void ContextSensitive_BypassesBody()
    {
        var f = new CandidateFilter(EmptyPosMatcher());
        var nodes = new List<Node> { N("た", "田", 1, 1), N("なか", "中", 2, 2) };
        var cand = C("たなか", "田中", 999999, 999999, 1, 2);
        cand.Attributes |= Candidate.Attribute.ContextSensitive;
        Assert.Equal(CandidateFilter.ResultType.GoodCandidate,
            f.FilterCandidate(Conv(), "たなか", cand, nodes, nodes));
    }
}
