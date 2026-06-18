using Mozc.Session;
using Xunit;

namespace Mozc.Session.Tests;

public class KeymapPresetsTests
{
    private static string DataDir()
    {
        var dir = new global::System.IO.DirectoryInfo(global::System.AppContext.BaseDirectory);
        while (dir != null)
        {
            string c = global::System.IO.Path.Combine(dir.FullName, "src", "data");
            if (global::System.IO.Directory.Exists(global::System.IO.Path.Combine(c, "keymap")))
            {
                return c;
            }
            dir = dir.Parent;
        }
        return string.Empty;
    }

    [Theory]
    [InlineData("MSIME", "ms-ime.tsv")]
    [InlineData("ATOK", "atok.tsv")]
    [InlineData("KOTOERI", "kotoeri.tsv")]
    [InlineData("CUSTOM", null)]
    [InlineData("NONE", null)]
    public void FileNameFor_MapsKnownPresets(string name, string? expected)
        => Assert.Equal(expected, KeymapPresets.FileNameFor(name));

    [Fact]
    public void Load_RealMsImePreset()
    {
        string dataDir = DataDir();
        Assert.True(dataDir.Length > 0, "src/data/keymap not found");
        KeyMap? km = KeymapPresets.Load(dataDir, "MSIME");
        Assert.NotNull(km);
        Assert.True(km!.EntryCount > 50);
        // ms-ime.tsv: Composition + Backspace → Backspace コマンド。
        Assert.Equal("Backspace", km.GetCommand("Composition", "Backspace"));
    }
}
