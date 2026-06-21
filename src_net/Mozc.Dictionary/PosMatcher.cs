using System.Buffers.Binary;

namespace Mozc.Dictionary;

// C++ src/dictionary/pos_matcher.h 相当(データ駆動部)。
// data レイアウト(uint16 配列):
//   [0, lidTableSize)              : 各規則の関数ID(GetXxxId が返す lid)
//   [lidTableSize + ruleIndex]     : その規則のレンジ表への offset(uint16 単位)
//   レンジ表: (lo, hi) ペアの並び、0xFFFF 終端。id が lo<=id<=hi なら一致。
// 名前付きアクセサ(IsFunctional 等)の規則 index は src/data/rules/pos_matcher_rule.def
// の記載順(C++ では gen_pos_matcher_code.py が pos_matcher_impl.inc を生成)。
public sealed class PosMatcher
{
    // pos_matcher_rule.def の記載順 = 規則 index(全35規則)。RuleCount=kLidTableSize。
    public enum Rule
    {
        Functional = 0,
        Unknown,
        FirstName,
        LastName,
        Number,
        KanjiNumber,
        WeakCompoundNounPrefix,
        WeakCompoundVerbPrefix,
        WeakCompoundFillerPrefix,
        WeakCompoundNounSuffix,
        WeakCompoundVerbSuffix,
        AcceptableParticleAtBeginOfSegment,
        JapanesePunctuations,
        OpenBracket,
        CloseBracket,
        GeneralSymbol,
        Zipcode,
        IsolatedWord,
        SuggestOnlyWord,
        ContentWordWithConjugation,
        SuffixWord,
        CounterSuffixWord,
        UniqueNoun,
        GeneralNoun,
        Pronoun,
        ContentNoun,
        NounPrefix,
        EosSymbol,
        Adverb,
        AdverbSegmentSuffix,
        ParallelMarker,
        TeSuffix,
        VerbSuffix,
        KagyoTaConnectionVerb,
        WagyoRenyoConnectionVerb,
    }

    public const int RuleCount = 35;

    private readonly ushort[] _data;
    private readonly int _lidTableSize;

    public PosMatcher(ushort[] data, int lidTableSize)
    {
        _data = data;
        _lidTableSize = lidTableSize;
    }

    // pos_matcher.data (uint16 LE バイト列) から構築。
    public static PosMatcher FromBytes(ReadOnlySpan<byte> bytes, int lidTableSize)
    {
        var data = new ushort[bytes.Length / 2];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i * 2));
        }
        return new PosMatcher(data, lidTableSize);
    }

    // 規則 index の関数ID(C++ の GetXxxId 相当)。
    public ushort GetId(int ruleIndex) => _data[ruleIndex];

    // id が規則 ruleIndex のレンジ表に含まれるか(C++ IsRuleInTable 相当)。
    public bool IsRuleInTable(int ruleIndex, ushort id)
    {
        int offset = _data[_lidTableSize + ruleIndex];
        for (int p = offset; _data[p] != 0xFFFF; p += 2)
        {
            if (id >= _data[p] && id <= _data[p + 1])
            {
                return true;
            }
        }
        return false;
    }

    // --- 名前付きアクセサ(pos_matcher_impl.inc 相当, 全35規則の GetXxxId/IsXxx) ---
    public ushort GetId(Rule rule) => _data[(int)rule];

    public ushort GetFunctionalId() => GetId(Rule.Functional);
    public bool IsFunctional(ushort id) => IsRuleInTable((int)Rule.Functional, id);
    public ushort GetUnknownId() => GetId(Rule.Unknown);
    public bool IsUnknown(ushort id) => IsRuleInTable((int)Rule.Unknown, id);
    public ushort GetFirstNameId() => GetId(Rule.FirstName);
    public bool IsFirstName(ushort id) => IsRuleInTable((int)Rule.FirstName, id);
    public ushort GetLastNameId() => GetId(Rule.LastName);
    public bool IsLastName(ushort id) => IsRuleInTable((int)Rule.LastName, id);
    public ushort GetNumberId() => GetId(Rule.Number);
    public bool IsNumber(ushort id) => IsRuleInTable((int)Rule.Number, id);
    public ushort GetKanjiNumberId() => GetId(Rule.KanjiNumber);
    public bool IsKanjiNumber(ushort id) => IsRuleInTable((int)Rule.KanjiNumber, id);
    public ushort GetWeakCompoundNounPrefixId() => GetId(Rule.WeakCompoundNounPrefix);
    public bool IsWeakCompoundNounPrefix(ushort id) => IsRuleInTable((int)Rule.WeakCompoundNounPrefix, id);
    public ushort GetWeakCompoundVerbPrefixId() => GetId(Rule.WeakCompoundVerbPrefix);
    public bool IsWeakCompoundVerbPrefix(ushort id) => IsRuleInTable((int)Rule.WeakCompoundVerbPrefix, id);
    public ushort GetWeakCompoundFillerPrefixId() => GetId(Rule.WeakCompoundFillerPrefix);
    public bool IsWeakCompoundFillerPrefix(ushort id) => IsRuleInTable((int)Rule.WeakCompoundFillerPrefix, id);
    public ushort GetWeakCompoundNounSuffixId() => GetId(Rule.WeakCompoundNounSuffix);
    public bool IsWeakCompoundNounSuffix(ushort id) => IsRuleInTable((int)Rule.WeakCompoundNounSuffix, id);
    public ushort GetWeakCompoundVerbSuffixId() => GetId(Rule.WeakCompoundVerbSuffix);
    public bool IsWeakCompoundVerbSuffix(ushort id) => IsRuleInTable((int)Rule.WeakCompoundVerbSuffix, id);
    public ushort GetAcceptableParticleAtBeginOfSegmentId() => GetId(Rule.AcceptableParticleAtBeginOfSegment);
    public bool IsAcceptableParticleAtBeginOfSegment(ushort id) => IsRuleInTable((int)Rule.AcceptableParticleAtBeginOfSegment, id);
    public ushort GetJapanesePunctuationsId() => GetId(Rule.JapanesePunctuations);
    public bool IsJapanesePunctuations(ushort id) => IsRuleInTable((int)Rule.JapanesePunctuations, id);
    public ushort GetOpenBracketId() => GetId(Rule.OpenBracket);
    public bool IsOpenBracket(ushort id) => IsRuleInTable((int)Rule.OpenBracket, id);
    public ushort GetCloseBracketId() => GetId(Rule.CloseBracket);
    public bool IsCloseBracket(ushort id) => IsRuleInTable((int)Rule.CloseBracket, id);
    public ushort GetGeneralSymbolId() => GetId(Rule.GeneralSymbol);
    public bool IsGeneralSymbol(ushort id) => IsRuleInTable((int)Rule.GeneralSymbol, id);
    public ushort GetZipcodeId() => GetId(Rule.Zipcode);
    public bool IsZipcode(ushort id) => IsRuleInTable((int)Rule.Zipcode, id);
    public ushort GetIsolatedWordId() => GetId(Rule.IsolatedWord);
    public bool IsIsolatedWord(ushort id) => IsRuleInTable((int)Rule.IsolatedWord, id);
    public ushort GetSuggestOnlyWordId() => GetId(Rule.SuggestOnlyWord);
    public bool IsSuggestOnlyWord(ushort id) => IsRuleInTable((int)Rule.SuggestOnlyWord, id);
    public ushort GetContentWordWithConjugationId() => GetId(Rule.ContentWordWithConjugation);
    public bool IsContentWordWithConjugation(ushort id) => IsRuleInTable((int)Rule.ContentWordWithConjugation, id);
    public ushort GetSuffixWordId() => GetId(Rule.SuffixWord);
    public bool IsSuffixWord(ushort id) => IsRuleInTable((int)Rule.SuffixWord, id);
    public ushort GetCounterSuffixWordId() => GetId(Rule.CounterSuffixWord);
    public bool IsCounterSuffixWord(ushort id) => IsRuleInTable((int)Rule.CounterSuffixWord, id);
    public ushort GetUniqueNounId() => GetId(Rule.UniqueNoun);
    public bool IsUniqueNoun(ushort id) => IsRuleInTable((int)Rule.UniqueNoun, id);
    public ushort GetGeneralNounId() => GetId(Rule.GeneralNoun);
    public bool IsGeneralNoun(ushort id) => IsRuleInTable((int)Rule.GeneralNoun, id);
    public ushort GetPronounId() => GetId(Rule.Pronoun);
    public bool IsPronoun(ushort id) => IsRuleInTable((int)Rule.Pronoun, id);
    public ushort GetContentNounId() => GetId(Rule.ContentNoun);
    public bool IsContentNoun(ushort id) => IsRuleInTable((int)Rule.ContentNoun, id);
    public ushort GetNounPrefixId() => GetId(Rule.NounPrefix);
    public bool IsNounPrefix(ushort id) => IsRuleInTable((int)Rule.NounPrefix, id);
    public ushort GetEosSymbolId() => GetId(Rule.EosSymbol);
    public bool IsEosSymbol(ushort id) => IsRuleInTable((int)Rule.EosSymbol, id);
    public ushort GetAdverbId() => GetId(Rule.Adverb);
    public bool IsAdverb(ushort id) => IsRuleInTable((int)Rule.Adverb, id);
    public ushort GetAdverbSegmentSuffixId() => GetId(Rule.AdverbSegmentSuffix);
    public bool IsAdverbSegmentSuffix(ushort id) => IsRuleInTable((int)Rule.AdverbSegmentSuffix, id);
    public ushort GetParallelMarkerId() => GetId(Rule.ParallelMarker);
    public bool IsParallelMarker(ushort id) => IsRuleInTable((int)Rule.ParallelMarker, id);
    public ushort GetTeSuffixId() => GetId(Rule.TeSuffix);
    public bool IsTeSuffix(ushort id) => IsRuleInTable((int)Rule.TeSuffix, id);
    public ushort GetVerbSuffixId() => GetId(Rule.VerbSuffix);
    public bool IsVerbSuffix(ushort id) => IsRuleInTable((int)Rule.VerbSuffix, id);
    public ushort GetKagyoTaConnectionVerbId() => GetId(Rule.KagyoTaConnectionVerb);
    public bool IsKagyoTaConnectionVerb(ushort id) => IsRuleInTable((int)Rule.KagyoTaConnectionVerb, id);
    public ushort GetWagyoRenyoConnectionVerbId() => GetId(Rule.WagyoRenyoConnectionVerb);
    public bool IsWagyoRenyoConnectionVerb(ushort id) => IsRuleInTable((int)Rule.WagyoRenyoConnectionVerb, id);
}
