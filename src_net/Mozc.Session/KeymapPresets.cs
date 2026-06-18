namespace Mozc.Session;

// Config.SessionKeymap に対応するプリセット keymap ファイル名の解決。
// (Config enum への直接依存を避けるため文字列名で扱う。呼び出し側が enum→名前を渡す)
public static class KeymapPresets
{
    // C++ keymap の OriginalName(MSIME/ATOK/KOTOERI/MOBILE/CHROMEOS)→ src/data/keymap のファイル名。
    public static string? FileNameFor(string sessionKeymapName) => sessionKeymapName switch
    {
        "MSIME" => "ms-ime.tsv",
        "ATOK" => "atok.tsv",
        "KOTOERI" => "kotoeri.tsv",
        "MOBILE" => "mobile.tsv",
        "CHROMEOS" => "chromeos.tsv",
        _ => null, // CUSTOM/NONE 等はプリセット無し(カスタムテーブルを使う)。
    };

    // dataDir/keymap/<file> を解決(存在すればフルパス、無ければ null)。
    public static string? ResolvePath(string dataDir, string sessionKeymapName)
    {
        string? file = FileNameFor(sessionKeymapName);
        if (file == null)
        {
            return null;
        }
        string path = global::System.IO.Path.Combine(dataDir, "keymap", file);
        return global::System.IO.File.Exists(path) ? path : null;
    }

    // プリセットを読み込んだ KeyMap を返す(解決不可なら null)。
    public static KeyMap? Load(string dataDir, string sessionKeymapName)
    {
        string? path = ResolvePath(dataDir, sessionKeymapName);
        if (path == null)
        {
            return null;
        }
        var km = new KeyMap();
        km.LoadFromFile(path);
        return km;
    }
}
