using Mozc.Session;
using Xunit;

namespace Mozc.Session.Tests;

public class KeyParserTests
{
    [Fact]
    public void ParsesModifierPlusChar()
    {
        Assert.True(KeyParser.TryParse("Ctrl @", out KeyEvent ke));
        Assert.Contains(ModifierKey.Ctrl, ke.Modifiers);
        Assert.Equal('@', ke.KeyCode);
        Assert.Null(ke.Special);
    }

    [Fact]
    public void ParsesSpecialKey()
    {
        Assert.True(KeyParser.TryParse("Shift Enter", out KeyEvent ke));
        Assert.Contains(ModifierKey.Shift, ke.Modifiers);
        Assert.Equal(SpecialKey.Enter, ke.Special);
    }

    [Fact]
    public void SignatureEquivalence()
    {
        KeyParser.TryParse("Ctrl i", out KeyEvent a);
        var b = new KeyEvent { KeyCode = 'i' };
        b.Modifiers.Add(ModifierKey.Ctrl);
        Assert.Equal(a.Signature(), b.Signature());
    }

    [Fact]
    public void RejectsUnknownKey()
    {
        Assert.False(KeyParser.TryParse("Ctrl Boguskey", out _));
    }

    [Theory]
    [InlineData("F13", SpecialKey.F13)]
    [InlineData("F24", SpecialKey.F24)]
    [InlineData("Multiply", SpecialKey.Multiply)]
    [InlineData("Add", SpecialKey.Add)]
    [InlineData("Decimal", SpecialKey.Decimal)]
    [InlineData("Divide", SpecialKey.Divide)]
    [InlineData("Comma", SpecialKey.Comma)]
    [InlineData("Clear", SpecialKey.Clear)]
    [InlineData("Equals", SpecialKey.Equals)]
    public void ParsesExtendedSpecialKeys(string input, SpecialKey expected)
    {
        Assert.True(KeyParser.TryParse(input, out KeyEvent ke));
        Assert.Equal(expected, ke.Special);
    }
}

public class KeyMapTests
{
    private static string FindRepoFile(string rel)
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        while (dir != null && !global::System.IO.File.Exists(global::System.IO.Path.Combine(dir.FullName, rel)))
        {
            dir = dir.Parent;
        }
        return global::System.IO.Path.Combine(dir!.FullName, rel);
    }

    [Fact]
    public void LoadsRealAtokKeymap_AndLooksUp()
    {
        var km = new KeyMap();
        km.LoadFromString(global::System.IO.File.ReadAllText(FindRepoFile("src/data/keymap/atok.tsv")));
        Assert.True(km.EntryCount > 50);

        // Composition + Ctrl i → ConvertToFullKatakana(atok.tsv より)。
        Assert.Equal("ConvertToFullKatakana", km.GetCommand("Composition", "Ctrl i"));
        // Composition + Backspace → Backspace。
        Assert.Equal("Backspace", km.GetCommand("Composition", "Backspace"));
        // 未定義キーは null。
        Assert.Null(km.GetCommand("Composition", "Ctrl Ctrl Ctrl z"));
    }

    [Fact]
    public void InlineKeymap_Lookup()
    {
        var km = new KeyMap();
        km.LoadFromString("Composition\tCtrl m\tCommit\nConversion\tSpace\tConvertNext");
        Assert.Equal("Commit", km.GetCommand("Composition", "Ctrl m"));
        Assert.Equal("ConvertNext", km.GetCommand("Conversion", "Space"));
        Assert.Null(km.GetCommand("Conversion", "Ctrl m"));
    }
}
