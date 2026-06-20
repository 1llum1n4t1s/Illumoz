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
        EmojiRewriter? emoji = BuildEmojiRewriter(dataDir);
        if (emoji != null)
        {
            merger.AddRewriter(emoji);
        }
        AddCommonRewriters(merger);
        return merger;
    }

    // mozc.data に記号/単漢字/絵文字が埋め込まれていればそれを使う rewriter 群。
    // (DataGen が SymbolTsv 等を梱包したデータ向け。ファイル直読みより優先したい場合に使用)
    public static IRewriter BuildDefaultRewriter(MozcEngine engine, IClock? clock = null)
    {
        var merger = new RewriterMerger();
        merger.AddRewriter(new DateRewriter(clock ?? new SystemClock()));
        merger.AddRewriter(new NumberRewriter());
        var symbol = engine.GetSymbolTable();
        merger.AddRewriter(symbol.Count > 0 ? new SymbolRewriter(symbol) : new SymbolRewriter());
        var sk = engine.GetSingleKanjiTable();
        if (sk.Count > 0)
        {
            merger.AddRewriter(new SingleKanjiRewriter(sk));
        }
        var emoji = engine.GetEmojiTable();
        if (emoji.Count > 0)
        {
            merger.AddRewriter(new EmojiRewriter(emoji));
        }
        AddCommonRewriters(merger);
        return merger;
    }

    // 埋め込みテーブル(mozc.data)を優先し、空のものだけ dataDir のファイルへフォールバックする。
    // --datadir を keymap プリセット公開のために渡したときも、記号/単漢字/絵文字の埋め込み
    // 変換を失わない(dataDir に symbol/single_kanji/emoji を stage していなくても動く)。
    public static IRewriter BuildDefaultRewriter(MozcEngine engine, string? dataDir, IClock? clock = null)
    {
        var merger = new RewriterMerger();
        merger.AddRewriter(new DateRewriter(clock ?? new SystemClock()));
        merger.AddRewriter(new NumberRewriter());

        var symbol = engine.GetSymbolTable();
        merger.AddRewriter(symbol.Count > 0 ? new SymbolRewriter(symbol) : BuildSymbolRewriter(dataDir));

        var sk = engine.GetSingleKanjiTable();
        SingleKanjiRewriter? skr = sk.Count > 0
            ? new SingleKanjiRewriter(sk)
            : BuildSingleKanjiRewriter(dataDir);
        if (skr != null)
        {
            merger.AddRewriter(skr);
        }

        var emoji = engine.GetEmojiTable();
        EmojiRewriter? er = emoji.Count > 0 ? new EmojiRewriter(emoji) : BuildEmojiRewriter(dataDir);
        if (er != null)
        {
            merger.AddRewriter(er);
        }

        AddCommonRewriters(merger);
        return merger;
    }

    // 記号/単漢字/絵文字より後段の、データソースに依存しない共通 rewriter 群(登録順固定)。
    private static void AddCommonRewriters(RewriterMerger merger)
    {
        merger.AddRewriter(new CalculatorRewriter());
        merger.AddRewriter(new UnicodeRewriter());
        merger.AddRewriter(new SmallLetterRewriter());
        merger.AddRewriter(new VariantsRewriter());
        merger.AddRewriter(new TransliterationRewriter());
        merger.AddRewriter(new EnglishVariantsRewriter());
        merger.AddRewriter(new CommandRewriter());
        merger.AddRewriter(new DiceRewriter());
        merger.AddRewriter(new CharacterFormRewriter());
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

    private static EmojiRewriter? BuildEmojiRewriter(string? dataDir)
    {
        string? p = ResolveData(dataDir, "emoji", "emoji_data.tsv");
        return p != null
            ? new EmojiRewriter(EmojiRewriter.LoadTable(global::System.IO.File.ReadAllText(p)))
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
        // mozc.data の埋め込みテーブルを優先し、空のものだけ dataDir のファイルへフォールバックする。
        // (--datadir を keymap プリセット用に渡しても記号/単漢字/絵文字の埋め込み変換を失わない)
        IRewriter rewriter = BuildDefaultRewriter(engine, dataDir);
        return new EngineServer(engine, keyMap, rewriter, dataDir);
    }

    // プロファイルディレクトリ配下の標準ファイル名。
    public const string HistoryFile = "history.db";
    public const string UserDictionaryFile = "user_dictionary.db";
    public const string ConfigFile = "config1.db";
    public const string CharacterFormFile = "character_form.db";

    // OS 標準のユーザープロファイルディレクトリ。IPC の .ipc メタデータと同じ場所を使うため、
    // 共有の MozcPaths.GetUserProfileDirectory() に委譲する(C++ SystemUtil::GetUserProfileDirectory 相当)。
    // Windows: %LOCALAPPDATA%\Mozc / macOS: ~/Library/Application Support/Mozc /
    // Linux: $HOME/.mozc(あれば) or $XDG_CONFIG_HOME/mozc or ~/.config/mozc。
    // 以前は Windows で %APPDATA%(Roaming)を返し IPC 側(Local)と分裂していた。
    public static string DefaultProfileDir() => Mozc.Base.MozcPaths.GetUserProfileDirectory();

    // プロファイル(設定/履歴/ユーザー辞書)を dir から読み込む(無いファイルはスキップ)。
    public static void LoadProfile(EngineServer server, string dir)
    {
        if (server.Config.LoadFile(global::System.IO.Path.Combine(dir, ConfigFile)))
        {
            server.ApplyConfig(); // 読み込んだ設定を即反映。
        }
        server.Handler.LoadHistory(global::System.IO.Path.Combine(dir, HistoryFile));
        server.Handler.LoadUserDictionary(global::System.IO.Path.Combine(dir, UserDictionaryFile));
        // 文字形 LAST_FORM 記憶は ApplyConfig(マネージャ再構築)の後に読む。
        server.ConversionFormManager.LoadHistory(
            global::System.IO.Path.Combine(dir, CharacterFormFile));
    }

    // プロファイルを dir へ保存する(dir が無ければ作成)。
    public static void SaveProfile(EngineServer server, string dir)
    {
        global::System.IO.Directory.CreateDirectory(dir);
        server.Config.Save(global::System.IO.Path.Combine(dir, ConfigFile));
        server.Handler.SaveHistory(global::System.IO.Path.Combine(dir, HistoryFile));
        server.Handler.SaveUserDictionary(global::System.IO.Path.Combine(dir, UserDictionaryFile));
        server.ConversionFormManager.SaveHistory(
            global::System.IO.Path.Combine(dir, CharacterFormFile));
    }

    public static EngineServer CreateFromBytes(byte[] mozcData, string romanTable, string keymapTsv)
    {
        var keyMap = new KeyMap();
        keyMap.LoadFromString(keymapTsv);
        var engine = new MozcEngine(mozcData, romanTable);
        return new EngineServer(engine, keyMap, BuildDefaultRewriter(engine));
    }
}
