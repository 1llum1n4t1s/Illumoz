namespace Mozc.Session;

// C++ protocol/commands.proto の中核に対応する軽量モデル(POCO)。
// 厳密なワイヤー互換 protobuf は IPC server 着手時に Mozc.Protocol で生成する(C7 連携)。
public enum CommandType
{
    CreateSession,
    DeleteSession,
    SendKey,
    NoOperation,
}

public sealed class Input
{
    public CommandType Type { get; init; }
    public ulong SessionId { get; init; }
    public KeyEvent? Key { get; init; }
    public string KeyString { get; init; } = string.Empty;
}

public sealed class Output
{
    public ulong SessionId { get; init; }
    public bool Consumed { get; init; }
    public string Preedit { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty; // 確定文字列(あれば)
    public IReadOnlyList<string> Candidates { get; init; } = global::System.Array.Empty<string>();
    public bool ErrorOccured { get; init; }
}
