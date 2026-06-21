using System.Collections.Generic;
using Mozc.Base;
using Xunit;

namespace Mozc.Base.Tests;

public class GraphemeSplitterTests
{
    [Fact]
    public void Split_PlainKana()
    {
        Assert.Equal(new[] { "あ", "い", "う" }, GraphemeSplitter.Split("あいう"));
    }

    [Fact]
    public void Split_CombiningDakuten()
    {
        // U+304B(か) + U+3099(結合濁点) + U+304D(き) → 2 クラスタ。
        string s = "がき";
        List<string> g = GraphemeSplitter.Split(s);
        Assert.Equal(2, g.Count);
        Assert.Equal("が", g[0]); // 濁点が前のかへ結合
        Assert.Equal("き", g[1]);
    }

    [Fact]
    public void Split_EmojiZwjSequence()
    {
        // 👨 + ZWJ(U+200D) + 👩 → 1 クラスタ。
        string s = "\U0001F468‍\U0001F469";
        List<string> g = GraphemeSplitter.Split(s);
        Assert.Single(g);
        Assert.Equal(s, g[0]);
    }

    [Fact]
    public void Split_EmojiFlagSequence()
    {
        // 🇯 + 🇵 (regional indicators) → 1 クラスタ(日本国旗)。
        string s = "\U0001F1EF\U0001F1F5";
        Assert.Single(GraphemeSplitter.Split(s));
    }

    [Fact]
    public void Split_Ivs()
    {
        // 葛 + IVS(U+E0100) → 1 クラスタ。
        string s = "葛\U000E0100";
        Assert.Single(GraphemeSplitter.Split(s));
    }

    [Fact]
    public void Split_EmojiModifier()
    {
        // 👍 + 肌色修飾子(U+1F3FB) → 1 クラスタ。
        string s = "\U0001F44D\U0001F3FB";
        Assert.Single(GraphemeSplitter.Split(s));
    }

    [Fact]
    public void Split_Single()
    {
        Assert.Equal(new[] { "あ" }, GraphemeSplitter.Split("あ"));
    }
}
