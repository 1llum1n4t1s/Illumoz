using System.Collections.Generic;
using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class CsvUtilTests
{
    [Fact]
    public void SplitCsv_PlainFields()
    {
        Assert.Equal(new[] { "a", "b", "c" }, CsvUtil.SplitCsv("a,b,c"));
    }

    [Fact]
    public void SplitCsv_QuotedFields()
    {
        Assert.Equal(new[] { "よみ", "単語", "名詞" }, CsvUtil.SplitCsv("\"よみ\",\"単語\",\"名詞\""));
    }

    [Fact]
    public void SplitCsv_QuotedWithEmbeddedComma()
    {
        Assert.Equal(new[] { "a,b", "c" }, CsvUtil.SplitCsv("\"a,b\",c"));
    }

    [Fact]
    public void SplitCsv_EscapedQuote()
    {
        // "" → " (フィールド内のエスケープ)。
        Assert.Equal(new[] { "a\"b" }, CsvUtil.SplitCsv("\"a\"\"b\""));
    }

    [Fact]
    public void SplitCsv_SkipsLeadingWhitespace()
    {
        Assert.Equal(new[] { "a", "b" }, CsvUtil.SplitCsv("  a, \tb"));
    }

    [Fact]
    public void SplitCsv_TrailingComma_AddsEmpty()
    {
        Assert.Equal(new[] { "a", "" }, CsvUtil.SplitCsv("a,"));
    }
}
