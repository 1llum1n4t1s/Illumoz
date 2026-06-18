namespace Mozc.Session;

// C++ src/session/keymap.cc 相当(中核)。keymap.tsv(status\tkey\tcommand)を読み、
// (status, KeyEvent) → command を引く。
public sealed class KeyMap
{
    // status → (KeyEvent シグネチャ → command)
    private readonly Dictionary<string, Dictionary<string, string>> _map = new();

    public int EntryCount { get; private set; }

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

    // 指定 status・KeyEvent に対応するコマンド名(無ければ null)。
    public string? GetCommand(string status, KeyEvent keyEvent)
    {
        return _map.TryGetValue(status, out Dictionary<string, string>? byKey)
            && byKey.TryGetValue(keyEvent.Signature(), out string? command)
            ? command
            : null;
    }

    public string? GetCommand(string status, string keyString)
        => KeyParser.TryParse(keyString, out KeyEvent ke) ? GetCommand(status, ke) : null;
}
