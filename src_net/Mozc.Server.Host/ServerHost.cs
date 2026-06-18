using Mozc.Engine;
using Mozc.Rewriter;
using Mozc.Server;
using Mozc.Session;

namespace Mozc.Server.Host;

// mozc_server の組み立て(テスト可能)。mozc.data / ローマ字表 / keymap を読み EngineServer を作る。
public static class ServerHost
{
    // C++ rewriter.cc の登録順に倣った既定 rewriter 群(現状 Date/Calculator を有効化)。
    public static IRewriter BuildDefaultRewriter(IClock? clock = null)
    {
        var merger = new RewriterMerger();
        merger.AddRewriter(new DateRewriter(clock ?? new SystemClock()));
        merger.AddRewriter(new CalculatorRewriter());
        return merger;
    }

    public static EngineServer Create(string mozcDataPath, string romanTablePath, string keymapPath)
    {
        byte[] data = global::System.IO.File.ReadAllBytes(mozcDataPath);
        string romanTable = global::System.IO.File.ReadAllText(romanTablePath);
        var keyMap = new KeyMap();
        keyMap.LoadFromString(global::System.IO.File.ReadAllText(keymapPath));
        var engine = new MozcEngine(data, romanTable);
        return new EngineServer(engine, keyMap, BuildDefaultRewriter());
    }

    public static EngineServer CreateFromBytes(byte[] mozcData, string romanTable, string keymapTsv)
    {
        var keyMap = new KeyMap();
        keyMap.LoadFromString(keymapTsv);
        return new EngineServer(new MozcEngine(mozcData, romanTable), keyMap, BuildDefaultRewriter());
    }
}
