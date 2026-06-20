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
    public void TestSendKey_ActivatedFalse_DoesNotConsumeImeOffKeyInComposition()
    {
        // composition 活性中でも、クライアントが activated=false を宣言したキー(間接 IME off)は
        // SEND_KEY 側で素通しされる。TEST_SEND_KEY もこれと一致して「消費しない」を返すべき
        // (一致しないと IME がキーを横取りし、アプリの Space 等が効かなくなる)。
        var s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        // activated=false 宣言の Space は非消費(DirectInput 行に未バインド + active=false)。
        var spaceOff = new KeyEvent { Special = SpecialKey.Space, Activated = false };
        Assert.False(s.TestSendKey(spaceOff).Consumed);
        // 一方 activated=true 宣言なら従来どおり Composition の ConvertNext として消費する
        // (override が判定を変えていることの裏取り)。状態は TEST_SEND_KEY なので不変。
        var spaceOn = new KeyEvent { Special = SpecialKey.Space, Activated = true };
        Assert.True(s.TestSendKey(spaceOn).Consumed);
    }

    [Fact]
    public void TestSendKey_ActivatedNull_PreservesLegacyBehavior()
    {
        // activated 未宣言(null)のクライアントは従来挙動を維持する(_activated=true 既定なので
        // composition 中の Space は ConvertNext として消費)。
        var s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        var space = new KeyEvent { Special = SpecialKey.Space, Activated = null };
        Assert.True(s.TestSendKey(space).Consumed);
    }

    [Fact]
    public void InsertTextDirect_Precomposition_CommitsImmediately()
    {
        var s = NewSession();
        // 入力前(precomposition)の DIRECT_INPUT 直接テキストは即確定(Result)になる。
        SessionResult r = s.InsertTextDirect("あ", keyCode: null);
        Assert.True(r.Consumed);
        Assert.Equal("あ", r.Committed);
        Assert.Equal("", r.Preedit);
    }

    [Fact]
    public void InsertTextDirect_HalfWidthAscii_EchoesBackNotConsumed()
    {
        var s = NewSession();
        // 半角 ASCII 1 文字(key_code==key_string)は echo back(未消費・確定なし)。
        SessionResult r = s.InsertTextDirect("a", keyCode: 'a');
        Assert.False(r.Consumed);
        Assert.Equal("", r.Committed);
        Assert.Equal("", r.Preedit);
    }

    [Fact]
    public void InsertTextDirect_DuringComposition_ComposesAsIs()
    {
        var s = NewSession();
        foreach (char c in "wa")
        {
            s.SendKey(c.ToString()); // composition: わ
        }
        // 入力中(preedit)の DIRECT_INPUT は AS_IS 扱い: 即確定せず合成へ回る。
        SessionResult r = s.InsertTextDirect("z", keyCode: null);
        Assert.True(r.Consumed);
        Assert.Equal("", r.Committed);   // 即確定はしない
        Assert.NotEqual("", r.Preedit);  // preedit として保持される
    }

    [Fact]
    public void InsertTextDirect_DuringConversion_CommitsConversionThenText()
    {
        var s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        s.SendKey("Space"); // ConvertNext → Convert(Conversion 状態へ)
        // 変換中の DIRECT_INPUT は、変換を確定してから直接テキストを確定する。
        SessionResult r = s.InsertTextDirect("x", keyCode: null);
        Assert.True(r.Consumed);
        Assert.Equal("私x", r.Committed);
        Assert.Equal("", r.Preedit);
    }

    [Fact]
    public void TestInsertTextDirect_Precomposition_NotConsumed()
    {
        var s = NewSession();
        // TEST_SEND_KEY: precomposition の DIRECT_INPUT は echo back 扱いで未消費(状態不変)。
        Assert.False(s.TestInsertTextDirect("あ", keyCode: null).Consumed);
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

    [Fact]
    public void SendKey_IndirectImeOff_CommitsPendingPreedit()
    {
        // 間接 IME OFF(activated=false 宣言キー)は、入力中 preedit を確定してから無効化する。
        // 確定せず _activated だけ落とすと打鍵が _typed に隠れ、後続 ON で stale テキストが復活する。
        Session s = NewSession();
        foreach (char c in "watashi")
        {
            s.SendKey(c.ToString());
        }
        SessionResult r = s.SendKey(new KeyEvent { Activated = false });
        Assert.True(r.Consumed);
        Assert.Equal("わたし", r.Committed);
        Assert.Equal("", r.Preedit);
        Assert.False(s.Activated);
    }

    [Fact]
    public void InsertTextAsIs_KeepsLiteral_NoRomajiConversion()
    {
        // AS_IS の key_string はローマ字表変換をかけず literal を保持する。
        Session s = NewSession();
        SessionResult r = s.InsertTextAsIs("e");
        Assert.True(r.Consumed);
        Assert.Equal("e", r.Preedit);
        // 対比: 通常の生テキスト経路はローマ字規則 e→え を適用する。
        Assert.Equal("え", NewSession().InsertText("e").Preedit);
    }

    [Fact]
    public void InsertTextDirect_DuringComposition_KeepsLiteral_NoRomaji()
    {
        // preedit 中の DIRECT_INPUT は AS_IS と同じ扱い。ローマ字表変換せず literal を合成する。
        Session s = NewSession();
        s.SendKey("w");
        s.SendKey("a"); // preedit わ
        Assert.Equal("わ", s.GetPreedit());
        SessionResult r = s.InsertTextDirect("e", keyCode: null);
        Assert.True(r.Consumed);
        // 'e' はローマ字規則 e→え を適用せず literal で付く(わe であって わえ ではない)。
        Assert.Equal("わe", s.GetPreedit());
    }

    [Fact]
    public void PartialCommit_ThenBackspace_DoesNotReintroduceCommittedHead()
    {
        Session s = NewSession();
        foreach (char c in "watashinonamae")
        {
            s.SendKey(c.ToString());
        }
        s.SendKey("Space"); // 変換開始(私 の 名前)
        Assert.True(s.Converter.ConversionSegmentsSize >= 2);

        // SUBMIT_CANDIDATE で先頭文節のみ部分確定し、残りは変換継続。
        SessionResult r = s.SendCommand(SessionCommandType.SubmitCandidate, 0);
        Assert.Equal("私", r.Committed);
        Assert.Equal(SessionConverter.State.Conversion, s.Converter.CurrentState);

        // 残りをキャンセルして composition へ戻すと、preedit は残り読みのみ(先頭は含まない)。
        s.SendKey("Escape");
        Assert.DoesNotContain("私", s.GetPreedit());

        // Backspace で末尾を削っても、_typed 再同期により既確定の先頭が復活しない。
        s.SendKey("Backspace");
        Assert.DoesNotContain("私", s.GetPreedit());
    }
}
