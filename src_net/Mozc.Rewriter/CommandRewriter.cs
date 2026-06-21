using System.Collections.Generic;
using Mozc.Converter;

namespace Mozc.Rewriter;

// C++ src/rewriter/command_rewriter.cc 相当。
// 特定の読み(例「しーくれっと」「さじぇすと」)のとき、設定変更コマンド候補
// (シークレットモード ON/OFF・サジェスト一時停止/復帰)を挿入する。
// 設定状態(incognito/presentation/サジェスト有効)に応じて ON↔OFF を切替える。
public sealed class CommandRewriter : IRewriter
{
    private const string Prefix = "【";
    private const string Suffix = "】";
    private const string Description = "設定を変更します";

    private const string IncognitoOn = "シークレットモードをオン";
    private const string IncognitoOff = "シークレットモードをオフ";
    private const string SuggestOff = "サジェスト機能の一時停止";
    private const string SuggestOn = "サジェスト機能を元に戻す";

    private static readonly HashSet<string> TriggerKeys = new()
    {
        "こまんど", "しーくれっと", "しーくれっともーど", "ひみつ",
        "ぷらいばしー", "ぷらいべーと", "さじぇすと", "ぷれぜんてーしょん",
        "ぷれぜん", "よそく", "よそくにゅうりょく", "よそくへんかん",
        "すいそくこうほ",
    };

    private static readonly HashSet<string> CommandValues = new() { "コマンド" };

    private static readonly HashSet<string> IncognitoValues = new()
    {
        "秘密", "シークレット", "シークレットモード", "プライバシー", "プライベート",
    };

    private static readonly HashSet<string> DisableSuggestionValues = new()
    {
        "サジェスト", "予測", "予測入力", "予測変換", "プレゼンテーション", "プレゼン",
    };

    // 設定状態(C++ config の該当フラグ相当)。既定は全 OFF / サジェスト有効。
    public bool IncognitoMode { get; set; }
    public bool PresentationMode { get; set; }
    public bool SuggestionEnabled { get; set; } = true;

    public bool Rewrite(Segments segments)
    {
        if (segments.ConversionSegmentsSize != 1)
        {
            return false;
        }
        Segment segment = segments.ConversionSegment(0);
        if (!TriggerKeys.Contains(segment.Key))
        {
            return false;
        }
        return RewriteSegment(segment);
    }

    private bool RewriteSegment(Segment segment)
    {
        for (int i = 0; i < segment.CandidatesSize; i++)
        {
            string value = segment.Get(i).Value;
            if (CommandValues.Contains(value))
            {
                InsertDisableSuggestionToggle(segment, i, 6);
                InsertIncognitoToggle(segment, i, 6);
                return true;
            }
            if (IncognitoValues.Contains(value))
            {
                InsertIncognitoToggle(segment, i, i + 3);
                return true;
            }
            if (DisableSuggestionValues.Contains(value))
            {
                InsertDisableSuggestionToggle(segment, i, i + 3);
                return true;
            }
        }
        return false;
    }

    private Candidate InsertCommandCandidate(Segment segment, int referencePos, int insertPos)
    {
        Candidate baseCand = segment.Get(referencePos);
        int at = global::System.Math.Min(segment.CandidatesSize, insertPos);
        var candidate = new Candidate
        {
            Key = baseCand.Key,
            Value = baseCand.Value,
            ContentKey = baseCand.ContentKey,
            ContentValue = baseCand.ContentValue,
            Cost = baseCand.Cost,
            Attributes = baseCand.Attributes | Candidate.Attribute.CommandCandidate | Candidate.Attribute.NoLearning,
            Description = Description,
            Prefix = Prefix,
            Suffix = Suffix,
        };
        segment.InsertCandidate(at, candidate);
        return candidate;
    }

    private void InsertIncognitoToggle(Segment segment, int referencePos, int insertPos)
    {
        Candidate c = InsertCommandCandidate(segment, referencePos, insertPos);
        if (IncognitoMode)
        {
            c.Value = IncognitoOff;
            c.Command = Candidate.CommandType.DisableIncognitoMode;
        }
        else
        {
            c.Value = IncognitoOn;
            c.Command = Candidate.CommandType.EnableIncognitoMode;
        }
        c.ContentValue = c.Value;
    }

    private void InsertDisableSuggestionToggle(Segment segment, int referencePos, int insertPos)
    {
        if (!SuggestionEnabled)
        {
            return;
        }
        Candidate c = InsertCommandCandidate(segment, referencePos, insertPos);
        if (PresentationMode)
        {
            c.Value = SuggestOn;
            c.Command = Candidate.CommandType.DisablePresentationMode;
        }
        else
        {
            c.Value = SuggestOff;
            c.Command = Candidate.CommandType.EnablePresentationMode;
        }
        c.ContentValue = c.Value;
    }
}
