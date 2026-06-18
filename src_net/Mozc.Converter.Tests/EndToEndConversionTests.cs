using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Xunit;

namespace Mozc.Converter.Tests;

// SystemDictionary(builder→reader)→ LatticeBuilder → Viterbi の縦串 end-to-end。
// 辞書・連接コストを制御し、最小コスト経路が期待どおり決まることを確認する。
public class EndToEndConversionTests
{
    // rsize=lsize=4、全遷移コスト一律 10(default=10)。
    private static Connector UniformConnector(ushort transition = 10)
    {
        const int n = 4;
        var def = new ushort[n];
        for (int i = 0; i < n; i++) def[i] = transition;
        var cost = new ushort[n][];
        for (int r = 0; r < n; r++)
        {
            cost[r] = new ushort[n];
            for (int l = 0; l < n; l++) cost[r][l] = transition;
        }
        return Connector.Create(ConnectorBuilder.Build(1, def, cost));
    }

    private static SystemDictionary Dict(params Token[] tokens)
        => new SystemDictionaryBuilder().Build(tokens);

    private static List<(string Key, string Value)> Convert(SystemDictionary dict, Connector conn, string key)
    {
        var lattice = new Lattice();
        lattice.SetKey(key);
        LatticeBuilder.PopulateFromDictionary(lattice, dict);
        Assert.True(new Viterbi(conn).Forward(lattice));
        return Viterbi.BestPath(lattice).Select(n => (n.Key, n.Value)).ToList();
    }

    [Fact]
    public void Convert_PrefersSingleWord_WhenCheaper()
    {
        // きょうと: 「京都」(一語,wcost100) vs 「今日」+「都」(50+50)。
        // 一語: 0+10+100=110, eos=120。分割: 60→120, eos=130。→ 京都が勝つ。
        var dict = Dict(
            new Token("きょうと", "京都", 100, 1, 1),
            new Token("きょう", "今日", 50, 2, 2),
            new Token("と", "都", 50, 3, 3));
        var path = Convert(dict, UniformConnector(), "きょうと");

        Assert.Single(path);
        Assert.Equal("京都", path[0].Value);
    }

    [Fact]
    public void Convert_PrefersSplit_WhenSingleWordExpensive()
    {
        // 京都の wcost を 300 に上げると分割(eos=130)が勝つ。
        var dict = Dict(
            new Token("きょうと", "京都", 300, 1, 1),
            new Token("きょう", "今日", 50, 2, 2),
            new Token("と", "都", 50, 3, 3));
        var path = Convert(dict, UniformConnector(), "きょうと");

        Assert.Equal(2, path.Count);
        Assert.Equal("今日", path[0].Value);
        Assert.Equal("都", path[1].Value);
    }

    [Fact]
    public void Convert_MultiSegmentSentence()
    {
        // わたしのなまえ → 私 の 名前(3語に分割)。
        var dict = Dict(
            new Token("わたし", "私", 100, 1, 1),
            new Token("の", "の", 50, 2, 2),
            new Token("なまえ", "名前", 100, 3, 3),
            // ノイズ(高コスト)で誤分割しないことを確認。
            new Token("わた", "綿", 900, 1, 1),
            new Token("し", "詩", 900, 1, 1));
        var path = Convert(dict, UniformConnector(), "わたしのなまえ");

        Assert.Equal(3, path.Count);
        Assert.Equal("私", path[0].Value);
        Assert.Equal("の", path[1].Value);
        Assert.Equal("名前", path[2].Value);
    }
}
