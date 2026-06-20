namespace Mozc.Session;

// C++ src/session/keymap.cc 相当(中核)。keymap.tsv(status\tkey\tcommand)を読み、
// (status, KeyEvent) → command を引く。
public sealed class KeyMap
{
    // status → (KeyEvent シグネチャ → command)
    private readonly Dictionary<string, Dictionary<string, string>> _map = new();

    public int EntryCount { get; private set; }

    // keymap tsv ファイルから読み込む(プリセット ms-ime.tsv 等)。
    public void LoadFromFile(string path)
        => LoadFromString(global::System.IO.File.ReadAllText(path));

    public void LoadFromString(string tsv)
    {
        foreach (string rawLine in tsv.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            string[] f = line.Split('\t');
            if (f.Length != 3)
            {
                continue;
            }
            string status = f[0];
            string keyStr = f[1];
            string command = f[2];
            // ヘッダ行(status\tkey\tcommand)はスキップ。
            if (status == "status" && keyStr == "key" && command == "command")
            {
                continue;
            }
            if (!KeyParser.TryParse(keyStr, out KeyEvent ke))
            {
                continue;
            }
            if (!_map.TryGetValue(status, out Dictionary<string, string>? byKey))
            {
                byKey = new Dictionary<string, string>();
                _map[status] = byKey;
            }
            byKey[ke.Signature()] = command;
            EntryCount++;
        }
    }

    // 既存マップを複製する。共有インスタンス(構築時 keymap)へ overlay を重ねると
    // 後続セッションへ汚染が残るため、overlay 適用前に複製して使う。
    public KeyMap Clone()
    {
        var copy = new KeyMap();
        foreach ((string status, Dictionary<string, string> byKey) in _map)
        {
            copy._map[status] = new Dictionary<string, string>(byKey);
            copy.EntryCount += byKey.Count;
        }
        return copy;
    }

    // 指定 status・KeyEvent に対応するコマンド名(無ければ null)。
    public string? GetCommand(string status, KeyEvent keyEvent)
    {
        string sig = keyEvent.Signature();
        if (_map.TryGetValue(status, out Dictionary<string, string>? byKey)
            && byKey.TryGetValue(sig, out string? command))
        {
            return command;
        }
        // Suggestion 状態は Composition を継承する(keymap tsv は Suggestion 固有行のみ持ち、
        // 通常の編集キーは Composition 行にある。フォールバックしないと入力中に backspace 等が
        // 効かなくなる)。
        if (status == "Suggestion"
            && _map.TryGetValue("Composition", out Dictionary<string, string>? comp)
            && comp.TryGetValue(sig, out string? inherited))
        {
            return inherited;
        }
        return null;
    }

    public string? GetCommand(string status, string keyString)
        => KeyParser.TryParse(keyString, out KeyEvent ke) ? GetCommand(status, ke) : null;
}
