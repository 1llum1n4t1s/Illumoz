namespace Mozc.Converter;

// C++ src/converter/candidate.h の Candidate 相当。変換候補1件。
public sealed class Candidate
{
    // C++ converter/attribute.h の Attribute(ビットフラグ)。
    [Flags]
    public enum Attribute : uint
    {
        Default = 0,
        BestCandidate = 1u << 0,
        Reranked = 1u << 1,
        NoHistoryLearning = 1u << 2,
        NoSuggestLearning = 1u << 3,
        NoLearning = (1u << 2) | (1u << 3),
        ContextSensitive = 1u << 4,
        SpellingCorrection = 1u << 5,
        NoVariantsExpansion = 1u << 6,
        NoExtraDescription = 1u << 7,
        RealtimeConversion = 1u << 8,
        UserDictionary = 1u << 9,
        CommandCandidate = 1u << 10,
        PartiallyKeyConsumed = 1u << 11,
        TypingCorrection = 1u << 12,
        AutoPartialSuggestion = 1u << 13,
        UserHistoryPrediction = 1u << 14,
        SuffixDictionary = 1u << 15,
        NoModification = 1u << 16,
        UserSegmentHistoryRewriter = 1u << 17,
        KeyExpandedInDictionary = 1u << 18,
        NoDeletable = 1u << 19,
        Unigram = 1u << 20,
        Bigram = 1u << 21,
        English = 1u << 22,
        Number = 1u << 23,
        SingleKanji = 1u << 24,
        TypingCompletion = 1u << 25,
        PostCorrection = 1u << 26,
        SupplementalModel = 1u << 27,
        WeakUserHistoryPrediction = 1u << 28,
        RealtimeTop = 1u << 29,
        DisableRescoring = 1u << 30,
    }

    public enum CategoryType
    {
        DefaultCategory,
        Symbol,
        Other,
    }

    public enum CommandType
    {
        DefaultCommand = 0,
        EnableIncognitoMode,
        DisableIncognitoMode,
        EnablePresentationMode,
        DisablePresentationMode,
    }

    public string Key = string.Empty;          // 読み
    public string Value = string.Empty;         // 表記
    public string ContentKey = string.Empty;
    public string ContentValue = string.Empty;
    public int ConsumedKeySize = 0;

    public string Prefix = string.Empty;
    public string Suffix = string.Empty;
    public string Description = string.Empty;
    public string A11yDescription = string.Empty;
    public string DisplayValue = string.Empty;

    public int UsageId = 0;
    public string UsageTitle = string.Empty;
    public string UsageDescription = string.Empty;

    public int Cost = 0;            // 文脈依存コスト(基本これでソート)
    public int Wcost = 0;           // 文脈自由コスト
    public int StructureCost = 0;   // 遷移のみのコスト

    public ushort Lid = 0;
    public ushort Rid = 0;

    public Attribute Attributes = Attribute.Default;
    public CategoryType Category = CategoryType.DefaultCategory;
    public CommandType Command = CommandType.DefaultCommand;

    public int CostBeforeRescoring = 0;

    // functional_key = key.substr(min(key.len, content_key.len))
    public string FunctionalKey =>
        ContentKey.Length >= Key.Length ? string.Empty : Key.Substring(ContentKey.Length);

    public string FunctionalValue =>
        ContentValue.Length >= Value.Length ? string.Empty : Value.Substring(ContentValue.Length);

    public void Clear()
    {
        Key = Value = ContentKey = ContentValue = string.Empty;
        ConsumedKeySize = 0;
        Prefix = Suffix = Description = A11yDescription = DisplayValue = string.Empty;
        UsageId = 0;
        UsageTitle = UsageDescription = string.Empty;
        Cost = Wcost = StructureCost = 0;
        Lid = Rid = 0;
        Attributes = Attribute.Default;
        Category = CategoryType.DefaultCategory;
        Command = CommandType.DefaultCommand;
        CostBeforeRescoring = 0;
    }

    public Candidate Clone() => (Candidate)MemberwiseClone();
}
