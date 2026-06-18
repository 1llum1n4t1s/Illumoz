using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Session;
using Xunit;

namespace Mozc.Session.Tests;

public class SessionTests
{
    private static List<string> PosRules()
    {
        var rules = new List<string> { "Functional ^助詞" };
        for (int i = 1; i < PosMatcher.RuleCount; i++)
        {
            rules.Add($"R{i} ^ZZ{i}");
        }
        return rules;
    }

    private const string RomanTable =
        "wa\tわ\nta\tた\nshi\tし\nno\tの\nna\tな\nma\tま\ne\tえ\nn\tん";

    private static MozcEngine Engine()
    {
        var sources = new DataSetBuilder.Sources
        {
            DictionaryLines = new[]
            {
                "わたし\t1\t1\t100\t私",
                "の\t2\t2\t50\tの",
                "なまえ\t1\t1\t100\t名前",
            },
            ConnectionLines = new[] { "3", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            ConnectionSpecialPosSize = 0,
            IdDefLines = new[]
            {
                "0 BOS/EOS,*,*,*,*,*,*",
                "1 名詞,一般,*,*,*,*,*",
                "2 助詞,格助詞,一般,*,*,*,*",
            },
            SpecialPosLines = global::System.Array.Empty<string>(),
            PosMatcherRuleLines = PosRules(),
            SegmenterRuleLines = global::System.Array.Empty<string>(),
            BoundaryDefLines = new[] { "PREFIX 助詞,格助詞, 1000" },
        };
        return new MozcEngine(new DataSetBuilder().Build(sources), RomanTable);
    }

    private static KeyMap Keymap()
    {
        var km = new KeyMap();
        km.LoadFromString(string.Join("\n", new[]
        {
            "Composition\tSpace\tConvertNext",
            "Conversion\tSpace\tConvertNext",
            "Composition\tEnter\tCommit",
            "Conversion\tEnter\tCommit",
            "Composition\tBackspace\tBackspace",
            "Conversion\tEscape\tCancel",
            "Precomposition\tCtrl Backspace\tUndo",
            "Composition\tF7\tConvertToFullKatakana",
            "Composition\tF8\tConvertToHalfWidth",
        }));
        return km;
    }

    private static Session NewSession() => new(Engine(), Keymap());

    [Fact]
    public void F7_ConvertsToFullKatakana_ViaKeymap()
    {
        var s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        SessionResult r = s.SendKey("F7");
        Assert.True(r.Consumed);
        Assert.Equal("ワタシ", r.Preedit);
        Assert.Equal("ワタシ", s.SendKey("Enter").Committed);
    }

    [Fact]
    public void F8_ConvertsToHalfKatakana_ViaKeymap()
    {
        var s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        Assert.Equal("ﾜﾀｼ", s.SendKey("F8").Preedit);
    }

    [Fact]
    public void GetSuggestions_DuringComposition()
    {
        var history = new Prediction.UserHistoryPredictor();
        var s = new Session(Engine(), Keymap(), rewriter: null, history: history);
        // 入力前は空。
        Assert.Empty(s.GetSuggestions());

        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        // わたし→私 が辞書サジェストに出る。
        Assert.Contains("私", s.GetSuggestions());

        // 確定後は composition でないので空。
        s.SendKey("Space");
        s.SendKey("Enter");
        Assert.Empty(s.GetSuggestions());
    }

    [Fact]
    public void Undo_RestoresCompositionAfterCommit()
    {
        Session s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        s.SendKey("Space");          // 変換
        SessionResult commit = s.SendKey("Enter"); // 確定
        Assert.Equal("私", commit.Committed);
        Assert.Equal("", s.GetPreedit());

        // Undo で確定前の composition(わたし)へ戻る。
        SessionResult undo = s.Undo();
        Assert.True(undo.Consumed);
        Assert.Equal("わたし", s.GetPreedit());

        // 2 回目の Undo は何もしない。
        Assert.False(s.Undo().Consumed);
    }

    [Fact]
    public void Undo_ViaKeymap_CtrlBackspace()
    {
        Session s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        s.SendKey("Space");
        s.SendKey("Enter");
        Assert.Equal("", s.GetPreedit());

        // Precomposition で Ctrl+Backspace → Undo コマンド。
        SessionResult r = s.SendKey("Ctrl Backspace");
        Assert.True(r.Consumed);
        Assert.Equal("わたし", s.GetPreedit());
    }

    [Fact]
    public void GetZeroQuerySuggestions_BeforeTyping()
    {
        var history = new Prediction.UserHistoryPredictor();
        history.Learn("もずく", "Mozc");
        var s = new Session(Engine(), Keymap(), rewriter: null, history: history);

        // 打鍵前は履歴のゼロクエリ候補が出る。
        Assert.Contains("Mozc", s.GetZeroQuerySuggestions());

        // 打鍵後はゼロクエリは空(通常サジェストへ切替)。
        s.SendKey("w");
        Assert.Empty(s.GetZeroQuerySuggestions());
    }

    [Fact]
    public void FullFlow_Type_Space_Enter()
    {
        Session s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        Assert.Equal("わたし", s.GetPreedit());

        SessionResult conv = s.SendKey("Space");
        Assert.True(conv.Consumed);
        Assert.Equal("私", conv.Preedit);

        SessionResult commit = s.SendKey("Enter");
        Assert.Equal("私", commit.Committed);
        Assert.Equal("", commit.Preedit);
    }

    [Fact]
    public void Backspace_RebuildsComposition()
    {
        Session s = NewSession();
        s.SendKey("w");
        s.SendKey("a");
        Assert.Equal("わ", s.GetPreedit());
        SessionResult r = s.SendKey("Backspace");
        Assert.True(r.Consumed);
        Assert.Equal("w", s.GetPreedit()); // 'a' を削除し 'w' 残り(pending)
    }

    [Fact]
    public void EscapeDuringConversion_Cancels()
    {
        Session s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        s.SendKey("Space");
        Assert.Equal("私", s.GetPreedit());
        s.SendKey("Escape");
        Assert.Equal("わたし", s.GetPreedit()); // かな保持で入力状態へ
    }

    [Fact]
    public void UnconsumedKey_WhenNothingToDo()
    {
        Session s = NewSession();
        // Precomposition で Enter は keymap 未定義(Composition のみ) → 消費しない。
        SessionResult r = s.SendKey("Enter");
        Assert.False(r.Consumed);
    }
}
