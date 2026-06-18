using Mozc.Composer;
using Mozc.Converter;
using Mozc.Dictionary;
using Mozc.Prediction;

namespace Mozc.Engine;

// C++ src/engine の Engine 相当(中核スライス)。mozc.data から DataManager を構築し、
// ImmutableConverter を保持。Composer(ローマ字入力)→ かなクエリ → 変換 を一気通貫で行う。
// 予測・Rewriter・Session 層は後続。
public sealed class MozcEngine
{
    private readonly DataManager _dataManager;
    private readonly ImmutableConverter _converter;
    private readonly PosMatcher _posMatcher;
    private readonly DictionaryPredictor _predictor;
    private Table _composerTable;

    public MozcEngine(byte[] mozcData, string romanTableTsv)
    {
        _dataManager = new DataManager(mozcData);
        _posMatcher = _dataManager.GetPosMatcher();
        _converter = new ImmutableConverter(
            _dataManager.GetSystemDictionary(),
            _dataManager.GetConnector(),
            _dataManager.GetSegmenter(),
            _posMatcher,
            new CandidateFilter(_posMatcher));

        _predictor = new DictionaryPredictor(
            _dataManager.GetSystemDictionary(), _dataManager.GetConnector(), _dataManager.GetSegmenter());

        _composerTable = new Table();
        _composerTable.LoadFromString(romanTableTsv);
    }

    public PosMatcher PosMatcher => _posMatcher;

    // ローマ字変換表を差し替える(Config.CustomRomanTable 反映用)。
    // 以降に CreateComposer する入力セッションへ反映される。
    public void SetRomanTable(string romanTableTsv)
    {
        var table = new Table();
        table.LoadFromString(romanTableTsv);
        _composerTable = table;
    }

    // mozc.data に埋め込まれた記号/単漢字/絵文字テーブル(無ければ空)。
    public IReadOnlyDictionary<string, string[]> GetSymbolTable() => _dataManager.GetStringMap("symbol");
    public IReadOnlyDictionary<string, string[]> GetSingleKanjiTable() => _dataManager.GetStringMap("single_kanji");
    public IReadOnlyDictionary<string, string[]> GetEmojiTable() => _dataManager.GetStringMap("emoji");

    // 新規 Composer を払い出す(入力セッション 1 つ分)。
    public Composer.Composer CreateComposer() => new(_composerTable);

    // かなクエリ文字列をそのまま変換する。
    public Segments Convert(string reading, int maxCandidates = 30)
        => _converter.Convert(reading, maxCandidates);

    // ローマ字入力済みの Composer から変換用クエリを取り出して変換する。
    public Segments ConvertFromComposer(Composer.Composer composer, int maxCandidates = 30)
        => Convert(composer.GetQueryForConversion(), maxCandidates);

    // 読み前方一致の予測候補(サジェスト)。
    public List<PredictionResult> Predict(string reading, int maxResults = 10)
        => _predictor.Predict(reading, maxResults);

    public List<PredictionResult> PredictFromComposer(Composer.Composer composer, int maxResults = 10)
        => Predict(composer.GetQueryForConversion(), maxResults);
}
