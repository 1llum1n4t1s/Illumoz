using Mozc.Base;
using Mozc.Dictionary;

namespace Mozc.Converter;

// C++ src/converter/candidate_filter.{h,cc} 相当。N-best 列挙時の候補品質ゲート。
// データ依存部は注入: isBadSuggestion(=SuggestionFilter), isSuppressedEntry/
// hasSuppressedEntries(=UserDictionary)。PosMatcher は実物。
public sealed class CandidateFilter
{
    public enum ResultType { GoodCandidate, BadCandidate, StopEnumeration }

    public enum RequestType { Conversion, ReverseConversion, Prediction, Suggestion }

    public sealed class Options
    {
        public RequestType RequestType = RequestType.Conversion;
        public bool ShouldFilterNoisyNumber;
    }

    private const int SizeThresholdForWeakCompound = 10;
    private const int MaxCandidatesSize = 200;
    private const int MinCost = 100;
    private const int CostOffset = 6907;
    private const int StructureCostOffset = 3453;
    private const int MinStructureCostOffset = 1151;
    private const int StopEnumerationCacheSize = 30;

    private readonly PosMatcher _posMatcher;
    private readonly Func<string, bool> _isBadSuggestion;
    private readonly Func<string, string, bool> _isSuppressedEntry;
    private readonly bool _hasSuppressedEntries;

    private readonly HashSet<(string Value, ushort Lid, ushort Rid)> _seen = new();
    private Candidate? _topCandidate;

    public CandidateFilter(PosMatcher posMatcher,
        Func<string, bool>? isBadSuggestion = null,
        Func<string, string, bool>? isSuppressedEntry = null,
        bool hasSuppressedEntries = false)
    {
        _posMatcher = posMatcher;
        _isBadSuggestion = isBadSuggestion ?? (_ => false);
        _isSuppressedEntry = isSuppressedEntry ?? ((_, _) => false);
        _hasSuppressedEntries = hasSuppressedEntries;
    }

    public void Reset()
    {
        _seen.Clear();
        _topCandidate = null;
    }

    private static (string, ushort, ushort) Id(Candidate c) => (c.Value, c.Lid, c.Rid);

    public ResultType FilterCandidate(Options options, string originalKey, Candidate candidate,
        IReadOnlyList<Node> topNodes, IReadOnlyList<Node> nodes)
    {
        if (options.RequestType == RequestType.ReverseConversion)
        {
            // 逆変換では重複除去のみ。
            return _seen.Add(Id(candidate)) ? ResultType.GoodCandidate : ResultType.BadCandidate;
        }
        ResultType result = FilterCandidateInternal(options, originalKey, candidate, topNodes, nodes);
        if (result != ResultType.GoodCandidate)
        {
            return result;
        }
        _seen.Add(Id(candidate));
        return result;
    }

    private ResultType CheckRequestType(Options options, string originalKey, Candidate candidate,
        IReadOnlyList<Node> nodes)
    {
        switch (options.RequestType)
        {
            case RequestType.Prediction:
                if (originalKey == candidate.Key)
                {
                    break;
                }
                goto case RequestType.Suggestion;
            case RequestType.Suggestion:
                if (_isBadSuggestion(candidate.Value))
                {
                    return ResultType.BadCandidate;
                }
                foreach (Node node in nodes)
                {
                    if (_isBadSuggestion(node.Value))
                    {
                        return ResultType.BadCandidate;
                    }
                }
                break;
        }
        return ResultType.GoodCandidate;
    }

    private ResultType FilterCandidateInternal(Options options, string originalKey,
        Candidate candidate, IReadOnlyList<Node> topNodes, IReadOnlyList<Node> nodes)
    {
        ResultType req = CheckRequestType(options, originalKey, candidate, nodes);
        if (req != ResultType.GoodCandidate)
        {
            return req;
        }

        // 制約ノードを含む top はコスト過大評価なので本体をスキップ。
        if ((candidate.Attributes & Candidate.Attribute.ContextSensitive) != 0)
        {
            return ResultType.GoodCandidate;
        }

        if (options.ShouldFilterNoisyNumber && IsNoisyNumberCandidate(nodes))
        {
            return ResultType.BadCandidate;
        }

        int candidateSize = _seen.Count;
        if (_topCandidate == null || candidateSize == 0)
        {
            _topCandidate = candidate;
        }

        // "短縮よみ"/"記号,一般" は 1 ノードのみ。
        if (nodes.Count > 1 && ContainsIsolatedWordOrGeneralSymbol(nodes))
        {
            return ResultType.BadCandidate;
        }
        if (IsIsolatedWordOrGeneralSymbol(nodes[0].Lid) &&
            (IsNormalOrConstrainedNode(nodes[0].Prev) || IsNormalOrConstrainedNode(nodes[0].Next)))
        {
            return ResultType.BadCandidate;
        }

        // 抑制単語の除去。
        if (_hasSuppressedEntries &&
            (_isSuppressedEntry(candidate.Key, candidate.Value) ||
             (candidate.Key != candidate.ContentKey && candidate.Value != candidate.ContentValue &&
              _isSuppressedEntry(candidate.ContentKey, candidate.ContentValue))))
        {
            return ResultType.BadCandidate;
        }

        // USER_DICTIONARY は重複除去しない。
        if ((candidate.Attributes & Candidate.Attribute.UserDictionary) != 0)
        {
            return ResultType.GoodCandidate;
        }

        if (candidateSize + 1 >= MaxCandidatesSize)
        {
            return ResultType.StopEnumeration;
        }

        if (_seen.Contains(Id(candidate)))
        {
            return ResultType.BadCandidate;
        }

        // 動詞活用の不正接続抑制("書います"/"買いて" 等)。
        if (ScriptClassifier.GetScriptType(nodes[0].Value) != ScriptType.Hiragana)
        {
            if (nodes.Count >= 2)
            {
                if (_posMatcher.IsKagyoTaConnectionVerb(nodes[0].Rid) &&
                    _posMatcher.IsVerbSuffix(nodes[1].Lid) && !_posMatcher.IsTeSuffix(nodes[1].Lid))
                {
                    return ResultType.BadCandidate;
                }
                if (_posMatcher.IsWagyoRenyoConnectionVerb(nodes[0].Rid) &&
                    _posMatcher.IsTeSuffix(nodes[1].Lid))
                {
                    return ResultType.BadCandidate;
                }
            }
            if (nodes[0].Lid != nodes[0].Rid)
            {
                if (_posMatcher.IsKagyoTaConnectionVerb(nodes[0].Lid) &&
                    _posMatcher.IsVerbSuffix(nodes[0].Rid) && !_posMatcher.IsTeSuffix(nodes[0].Rid))
                {
                    return ResultType.BadCandidate;
                }
                if (_posMatcher.IsWagyoRenyoConnectionVerb(nodes[0].Lid) &&
                    _posMatcher.IsTeSuffix(nodes[0].Rid))
                {
                    return ResultType.BadCandidate;
                }
            }
        }

        if (nodes.Count == 1)
        {
            return ResultType.GoodCandidate; // 単一トークンは落とさない。
        }
        if (ScriptClassifier.CharsLen(candidate.Value) == 1)
        {
            return ResultType.GoodCandidate; // 1 文字は落とさない。
        }

        bool isNoisyWeakCompound = IsNoisyWeakCompound(nodes);
        bool isConnectedWeakCompound = IsConnectedWeakCompound(nodes);
        if (isNoisyWeakCompound && candidateSize >= 1)
        {
            return ResultType.BadCandidate;
        }
        if (isConnectedWeakCompound && candidateSize >= SizeThresholdForWeakCompound)
        {
            return ResultType.BadCandidate;
        }

        // top と lid/rid が同じなら落とさない。
        if (!isNoisyWeakCompound && _topCandidate!.StructureCost == 0 &&
            candidate.Lid == _topCandidate.Lid && candidate.Rid == _topCandidate.Rid)
        {
            return ResultType.GoodCandidate;
        }

        // non-content 値が top と同じ(ひらがな)なら落とさない。
        string topNonContent = NonContentValue(_topCandidate!);
        string nonContent = NonContentValue(candidate);
        if (!isNoisyWeakCompound && !ReferenceEquals(_topCandidate, candidate) &&
            _topCandidate!.ContentValue != _topCandidate.Value &&
            ScriptClassifier.GetScriptType(topNonContent) == ScriptType.Hiragana &&
            topNonContent == nonContent)
        {
            return ResultType.GoodCandidate;
        }

        // カタカナ英語翻字チェック(realtime は除外)。
        if ((candidate.Attributes & Candidate.Attribute.RealtimeConversion) == 0)
        {
            bool isTopEnglishT13n =
                ScriptClassifier.GetScriptType(nodes[0].Key) == ScriptType.Hiragana &&
                ScriptClassifier.IsEnglishTransliteration(nodes[0].Value);
            for (int i = 1; i < nodes.Count; i++)
            {
                if (ScriptClassifier.GetScriptType(nodes[i].Key) == ScriptType.Hiragana &&
                    ScriptClassifier.IsEnglishTransliteration(nodes[i].Value))
                {
                    return ResultType.BadCandidate;
                }
                if (isTopEnglishT13n && !_posMatcher.IsFunctional(nodes[i].Lid))
                {
                    return ResultType.BadCandidate;
                }
            }
        }

        long topCost = global::System.Math.Max(MinCost, _topCandidate!.Cost);
        long topStructureCost = global::System.Math.Max(MinCost, _topCandidate.StructureCost);

        // 複合語 top で候補数<3 のときは積極フィルタしない。
        if (IsCompoundCandidate(topNodes) && candidateSize < 3 &&
            candidate.Cost < topCost + 2302 && candidate.StructureCost < 6907)
        {
            return ResultType.GoodCandidate;
        }

        // 人名は積極的に落とさない(構造コストのみで判定)。
        long costOffset = CostOffset;
        if (candidate.Lid == _posMatcher.GetLastNameId() || candidate.Lid == _posMatcher.GetFirstNameId())
        {
            costOffset = int.MaxValue - topCost;
        }

        if (topCost + costOffset < candidate.Cost &&
            topStructureCost + MinStructureCostOffset < candidate.StructureCost)
        {
            if (candidateSize < StopEnumerationCacheSize)
            {
                return ResultType.BadCandidate;
            }
            return ResultType.StopEnumeration;
        }

        if ((long)topStructureCost + StructureCostOffset > int.MaxValue ||
            global::System.Math.Max(topStructureCost, MinStructureCostOffset) + StructureCostOffset <
                candidate.StructureCost)
        {
            return ResultType.BadCandidate;
        }

        // 複数の数字ノード抑制("2十三重" 等)。
        if (nodes.Count >= 2)
        {
            int numberNodes = 0;
            ushort prevLid = 0;
            foreach (Node node in nodes)
            {
                if (ScriptClassifier.IsScriptType(node.Key, ScriptType.Numeric))
                {
                    prevLid = node.Lid;
                    continue;
                }
                ScriptType firstType = ScriptClassifier.GetFirstScriptType(node.Value, out int mblen);
                if (firstType == ScriptType.Numeric && prevLid != node.Lid)
                {
                    numberNodes++;
                }
                else if (firstType == ScriptType.Kanji)
                {
                    string firstKanji = node.Value.Substring(0, FirstCharLen(node.Value, mblen));
                    string converted = NumberUtil.KanjiNumberToArabicNumber(firstKanji);
                    if (firstKanji != converted && prevLid != node.Lid)
                    {
                        numberNodes++;
                    }
                }
                prevLid = node.Lid;
            }
            if (numberNodes >= 2)
            {
                return ResultType.BadCandidate;
            }
        }

        return ResultType.GoodCandidate;
    }

    // value の content_value 以降(非内容部)。
    private static string NonContentValue(Candidate c)
        => c.ContentValue.Length >= c.Value.Length ? string.Empty : c.Value.Substring(c.ContentValue.Length);

    // UTF-8 バイト長 mblen を C# string の char 数に変換(先頭1コードポイント分)。
    private static int FirstCharLen(string s, int _)
    {
        global::System.Text.Rune.DecodeFromUtf16(s, out global::System.Text.Rune rune, out int charsConsumed);
        return charsConsumed;
    }

    private bool IsNoisyWeakCompound(IReadOnlyList<Node> nodes)
    {
        if (nodes.Count <= 1)
        {
            return false;
        }
        if (nodes[0].Lid != nodes[0].Rid)
        {
            return false;
        }
        if (_posMatcher.IsWeakCompoundFillerPrefix(nodes[0].Lid))
        {
            return true;
        }
        if (nodes[1].Lid != nodes[1].Rid)
        {
            bool isPossibleAntiPhrase = _posMatcher.IsContentNoun(nodes[0].Rid) &&
                _posMatcher.IsAcceptableParticleAtBeginOfSegment(nodes[1].Lid);
            if (!isPossibleAntiPhrase)
            {
                return true;
            }
        }
        if (_posMatcher.IsWeakCompoundNounPrefix(nodes[0].Lid) &&
            !_posMatcher.IsWeakCompoundNounSuffix(nodes[1].Lid))
        {
            return true;
        }
        if (_posMatcher.IsWeakCompoundVerbPrefix(nodes[0].Lid) &&
            !_posMatcher.IsWeakCompoundVerbSuffix(nodes[1].Lid))
        {
            return true;
        }
        return false;
    }

    private bool IsConnectedWeakCompound(IReadOnlyList<Node> nodes)
    {
        if (nodes.Count <= 1)
        {
            return false;
        }
        if (nodes[0].Lid != nodes[0].Rid || nodes[1].Lid != nodes[1].Rid)
        {
            return false;
        }
        if (_posMatcher.IsWeakCompoundNounPrefix(nodes[0].Lid) &&
            _posMatcher.IsWeakCompoundNounSuffix(nodes[1].Lid))
        {
            return true;
        }
        if (_posMatcher.IsWeakCompoundVerbPrefix(nodes[0].Lid) &&
            _posMatcher.IsWeakCompoundVerbSuffix(nodes[1].Lid))
        {
            return true;
        }
        return false;
    }

    private bool IsIsolatedWordOrGeneralSymbol(ushort posId)
        => _posMatcher.IsIsolatedWord(posId) || _posMatcher.IsGeneralSymbol(posId);

    private bool ContainsIsolatedWordOrGeneralSymbol(IReadOnlyList<Node> nodes)
    {
        foreach (Node node in nodes)
        {
            if (IsIsolatedWordOrGeneralSymbol(node.Lid))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsNormalOrConstrainedNode(Node? node)
        => node != null && (node.Type == Node.NodeType.NorNode || node.Type == Node.NodeType.ConNode);

    private static bool IsCompoundCandidate(IReadOnlyList<Node> nodes)
        => nodes.Count == 1 && nodes[0].Lid != nodes[0].Rid;

    private bool IsNoisyNumberCandidate(IReadOnlyList<Node> nodes)
    {
        bool IsConvertedNumber(Node node)
        {
            if (node.Lid != node.Rid)
            {
                return false;
            }
            if (!ScriptClassifier.IsScriptType(node.Key, ScriptType.Hiragana))
            {
                return false;
            }
            return _posMatcher.IsNumber(node.Lid) || _posMatcher.IsKanjiNumber(node.Rid);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            if (!IsConvertedNumber(nodes[i]))
            {
                continue;
            }
            if (i + 1 < nodes.Count && !IsConvertedNumber(nodes[i + 1]) &&
                !_posMatcher.IsCounterSuffixWord(nodes[i + 1].Lid))
            {
                return true;
            }
            if (i - 1 >= 0 && _posMatcher.IsUniqueNoun(nodes[i - 1].Rid))
            {
                return true;
            }
        }
        return false;
    }
}
