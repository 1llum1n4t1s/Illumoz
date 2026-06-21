using Mozc.Dictionary;
using Xunit;
using T = Mozc.Dictionary.UserDictionaryImporter.ImeType;

namespace Mozc.Dictionary.Tests;

public class UserDictionaryImporterTests
{
    [Theory]
    [InlineData("!Microsoft IME Dictionary Tool", T.Msime)]
    [InlineData("!!DICUT11", T.Atok)]
    [InlineData("!!DICUT10", T.None)]      // 旧 ATOK は非対応
    [InlineData("!!ATOK_TANGO_TEXT_HEADER_1", T.Atok)]
    [InlineData("\"よみ\"", T.Kotoeri)]
    [InlineData("# Gboard Dictionary version:1", T.GboardV1)]
    [InlineData("よみ\t単語\t名詞", T.Mozc)]
    [InlineData("# comment", T.Mozc)]
    [InlineData("plain text", T.None)]
    public void GuessImeType_Detects(string line, T expected)
    {
        Assert.Equal(expected, UserDictionaryImporter.GuessImeType(line));
    }

    [Theory]
    [InlineData(T.AutoDetect, T.Msime, T.Msime)]    // 推測を信頼
    [InlineData(T.Mozc, T.Msime, T.Mozc)]            // Mozc は互換 → Mozc
    [InlineData(T.Mozc, T.Kotoeri, T.None)]          // Mozc だがことえり → 不可
    [InlineData(T.Atok, T.Atok, T.Atok)]             // 一致
    [InlineData(T.Atok, T.Msime, T.None)]            // 不一致
    public void DetermineFinalImeType(T user, T guessed, T expected)
    {
        Assert.Equal(expected, UserDictionaryImporter.DetermineFinalImeType(user, guessed));
    }

    [Fact]
    public void ParseLine_MozcTabSeparated()
    {
        var e = UserDictionaryImporter.ParseLine(T.Mozc, "ぐーぐる\tGoogle\t名詞\tメモ");
        Assert.NotNull(e);
        Assert.Equal("ぐーぐる", e!.Value.Reading);
        Assert.Equal("Google", e.Value.Word);
        Assert.Equal("名詞", e.Value.Pos);
        Assert.Equal("メモ", e.Value.Comment);
    }

    [Fact]
    public void ParseLine_GboardPrefixesPos()
    {
        var e = UserDictionaryImporter.ParseLine(T.GboardV1, "よみ\t単語\tja-JP");
        Assert.Equal("品詞なし:ja-JP", e!.Value.Pos);
    }

    [Fact]
    public void ParseLine_KotoeriCsv()
    {
        var e = UserDictionaryImporter.ParseLine(T.Kotoeri, "\"よみ\",\"単語\",\"名詞\"");
        Assert.Equal("よみ", e!.Value.Reading);
        Assert.Equal("単語", e.Value.Word);
        Assert.Equal("名詞", e.Value.Pos);
    }

    [Theory]
    [InlineData(T.Msime, "!comment line")]
    [InlineData(T.Mozc, "# comment")]
    [InlineData(T.Kotoeri, "// comment")]
    [InlineData(T.Mozc, "よみ\t単語")]      // フィールド不足
    [InlineData(T.Mozc, "")]
    public void ParseLine_SkipsCommentsAndShortLines(T type, string line)
    {
        Assert.Null(UserDictionaryImporter.ParseLine(type, line));
    }
}
