using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Engine;
using Mozc.Session;
using Xunit;

namespace Mozc.Session.Tests;

public class SessionConverterTests
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

    [Fact]
    public void Type_Convert_Commit_Flow()
    {
        var sc = new SessionConverter(Engine());
        foreach (char c in "watashinonamae")
        {
            sc.InsertCharacter(c.ToString());
        }
        Assert.Equal(SessionConverter.State.Composition, sc.CurrentState);
        Assert.Equal("わたしのなまえ", sc.GetPreedit());

        Assert.True(sc.Convert());
        Assert.Equal(SessionConverter.State.Conversion, sc.CurrentState);
        Assert.Equal("私の名前", sc.GetPreedit());

        string committed = sc.Commit();
        Assert.Equal("私の名前", committed);
        Assert.Equal(SessionConverter.State.Composition, sc.CurrentState);
        Assert.Equal("", sc.GetPreedit());
    }

    [Fact]
    public void Commit_LearnsHistory_ThenPredicts()
    {
        var history = new Prediction.UserHistoryPredictor();
        var sc = new SessionConverter(Engine(), rewriter: null, history: history);
        foreach (char c in "watashi")
        {
            sc.InsertCharacter(c.ToString());
        }
        Assert.True(sc.Convert());
        sc.Commit(); // 「私」を確定 → 履歴学習

        // 同じ読みを再入力すると履歴予測に現れる。
        foreach (char c in "watashi")
        {
            sc.InsertCharacter(c.ToString());
        }
        var preds = sc.PredictFromHistory();
        Assert.Contains(preds, p => p.Value == "私" && p.Key == "わたし");
    }

    [Fact]
    public void SelectByShortcut_SelectsCandidate()
    {
        var sc = new SessionConverter(Engine());
        foreach (char c in "watashi")
        {
            sc.InsertCharacter(c.ToString());
        }
        Assert.True(sc.Convert());

        // '1' → index 0 を選択(候補1件でも先頭は有効)。
        Assert.True(sc.SelectByShortcut('1', "123456789"));
        // 範囲外('2'→index1)・shortcuts に無い文字('x')は false。
        Assert.False(sc.SelectByShortcut('2', "123456789"));
        Assert.False(sc.SelectByShortcut('x', "123456789"));
    }

    [Fact]
    public void PredictMerged_HistoryRanksAboveDictionary()
    {
        var history = new Prediction.UserHistoryPredictor();
        history.Learn("わたし", "渡し"); // 辞書に無い表記を履歴に登録
        var sc = new SessionConverter(Engine(), rewriter: null, history: history);
        foreach (char c in "watashi")
        {
            sc.InsertCharacter(c.ToString());
        }

        var preds = sc.PredictMerged();
        var values = preds.ConvertAll(p => p.Value);
        Assert.Contains("渡し", values); // 履歴由来
        Assert.Contains("私", values);   // 辞書由来
        Assert.Equal("渡し", values[0]); // 履歴がブーストで上位
    }

    [Fact]
    public void SegmentFocus_AndCandidates()
    {
        var sc = new SessionConverter(Engine());
        foreach (char c in "watashinonamae")
        {
            sc.InsertCharacter(c.ToString());
        }
        sc.Convert();

        Assert.Equal(0, sc.FocusedSegment);
        sc.SegmentFocusRight();
        Assert.Equal(1, sc.FocusedSegment);
        sc.SegmentFocusLeft();
        Assert.Equal(0, sc.FocusedSegment);

        // 注目文節(私)の候補一覧に 私 が含まれる。
        Assert.Contains("私", sc.GetCandidates());
    }

    [Fact]
    public void SelectCandidate_ByIndex()
    {
        var sc = new SessionConverter(Engine());
        foreach (char c in "watashi")
        {
            sc.InsertCharacter(c.ToString());
        }
        // 変換前は選択不可。
        Assert.False(sc.SelectCandidate(0));
        sc.Convert();
        // 先頭候補を明示選択。
        Assert.True(sc.SelectCandidate(0));
        Assert.Equal("私", sc.GetPreedit());
        // 範囲外は false。
        Assert.False(sc.SelectCandidate(99));
    }

    [Fact]
    public void Cancel_ReturnsToComposition()
    {
        var sc = new SessionConverter(Engine());
        foreach (char c in "watashi")
        {
            sc.InsertCharacter(c.ToString());
        }
        sc.Convert();
        Assert.Equal(SessionConverter.State.Conversion, sc.CurrentState);
        sc.Cancel();
        Assert.Equal(SessionConverter.State.Composition, sc.CurrentState);
        // かなは保持。
        Assert.Equal("わたし", sc.GetPreedit());
    }

    [Fact]
    public void InsertDuringConversion_CommitsThenTypes()
    {
        var sc = new SessionConverter(Engine());
        foreach (char c in "watashi")
        {
            sc.InsertCharacter(c.ToString());
        }
        sc.Convert();
        // 変換中に新規入力 → 直前を確定し新規 composition。
        sc.InsertCharacter("n");
        sc.InsertCharacter("o");
        Assert.Equal(SessionConverter.State.Composition, sc.CurrentState);
        Assert.Equal("の", sc.GetPreedit());
    }
}
