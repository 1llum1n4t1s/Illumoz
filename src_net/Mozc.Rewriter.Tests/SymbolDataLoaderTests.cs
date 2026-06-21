using Mozc.Rewriter;
using Xunit;

namespace Mozc.Rewriter.Tests;

// 実 src/data の symbol.tsv / single_kanji.tsv をパースできることを検証。
public class SymbolDataLoaderTests
{
    // テスト実行ディレクトリ(bin/Release/netX)から repo ルートへ遡って data を探す。
    private static string DataPath(string rel)
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = global::System.IO.Path.Combine(dir.FullName, "src", "data", rel);
            if (global::System.IO.File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return string.Empty;
    }

    [Fact]
    public void Symbol_ParsesRealTsv()
    {
        string path = DataPath(global::System.IO.Path.Combine("symbol", "symbol.tsv"));
        Assert.True(global::System.IO.File.Exists(path), $"symbol.tsv not found: {path}");
        var table = SymbolRewriter.LoadTable(global::System.IO.File.ReadAllText(path));
        Assert.NotEmpty(table);
        // 「まる」読みに記号「。」が含まれる(symbol.tsv の句点行)。
        Assert.True(table.ContainsKey("まる"));
        Assert.Contains("。", table["まる"]);
    }

    [Fact]
    public void SingleKanji_ParsesRealTsv()
    {
        string path = DataPath(global::System.IO.Path.Combine("single_kanji", "single_kanji.tsv"));
        Assert.True(global::System.IO.File.Exists(path), $"single_kanji.tsv not found: {path}");
        var table = SingleKanjiRewriter.LoadTable(global::System.IO.File.ReadAllText(path));
        Assert.NotEmpty(table);
        // 「あ」読みに「亜」が含まれる。
        Assert.True(table.ContainsKey("あ"));
        Assert.Contains("亜", table["あ"]);
    }
}
