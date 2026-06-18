using Mozc.Engine;
using Mozc.Rewriter;
using Mozc.Server;
using Mozc.Session;

namespace Mozc.Server.Host;

// mozc_server の組み立て(テスト可能)。mozc.data / ローマ字表 / keymap を読み EngineServer を作る。
public static class ServerHost
{
    // C++ rewriter.cc の登録順に倣った既定 rewriter 群。dataDir が指定され
    // symbol.tsv / single_kanji.tsv が存在すれば実データ由来のテーブルを使う。
    public static IRewriter BuildDefaultRewriter(IClock? clock = null, string? dataDir = null)
    {
        var merger = new RewriterMerger();
        merger.AddRewriter(new DateRewriter(clock ?? new SystemClock()));
        merger.AddRewriter(new NumberRewriter());
        merger.AddRewriter(BuildSymbolRewriter(dataDir));
        SingleKanjiRewriter? sk = BuildSingleKanjiRewriter(dataDir);
        if (sk != null)
        {
            merger.AddRewriter(sk);
        }
        merger.AddRewriter(new CalculatorRewriter());
        merger.AddRewriter(new TransliterationRewriter());
        return merger;
    }

    private static SymbolRewriter BuildSymbolRewriter(string? dataDir)
    {
        string? p = ResolveData(dataDir, "symbol", "symbol.tsv");
        return p != null
            ? new SymbolRewriter(SymbolRewriter.LoadTable(global::System.IO.File.ReadAllText(p)))
            : new SymbolRewriter();
    }

    private static SingleKanjiRewriter? BuildSingleKanjiRewriter(string? dataDir)
    {
        string? p = ResolveData(dataDir, "single_kanji", "single_kanji.tsv");
        return p != null
            ? new SingleKanjiRewriter(SingleKanjiRewriter.LoadTable(global::System.IO.File.ReadAllText(p)))
            : null;
    }

    private static string? ResolveData(string? dataDir, string sub, string file)
    {
        if (dataDir == null)
        {
            return null;
        }
        string path = global::System.IO.Path.Combine(dataDir, sub, file);
        return global::System.IO.File.Exists(path) ? path : null;
    }

    public static EngineServer Create(string mozcDataPath, string romanTablePath, string keymapPath,
        string? dataDir = null)
    {
        byte[] data = global::System.IO.File.ReadAllBytes(mozcDataPath);
        string romanTable = global::System.IO.File.ReadAllText(romanTablePath);
        var keyMap = new KeyMap();
        keyMap.LoadFromString(global::System.IO.File.ReadAllText(keymapPath));
        var engine = new MozcEngine(data, romanTable);
        return new EngineServer(engine, keyMap, BuildDefaultRewriter(dataDir: dataDir));
    }

    public static EngineServer CreateFromBytes(byte[] mozcData, string romanTable, string keymapTsv)
    {
        var keyMap = new KeyMap();
        keyMap.LoadFromString(keymapTsv);
        return new EngineServer(new MozcEngine(mozcData, romanTable), keyMap, BuildDefaultRewriter());
    }
}
