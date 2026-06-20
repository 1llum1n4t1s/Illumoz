namespace Mozc.Session;

// C++ protocol/commands.proto の中核に対応する軽量モデル(POCO)。
// 厳密なワイヤー互換 protobuf は IPC server 着手時に Mozc.Protocol で生成する(C7 連携)。
public enum CommandType
{
    CreateSession,
    DeleteSession,
    SendKey,
    TestSendKey,
    SendCommand,
    NoOperation,
    GetConfig,
    SetConfig,
    ClearUserHistory,
    ClearUserPrediction,
    ClearUnusedUserPrediction,
    Reload,
    SyncData,
}

// 全セッション共有の挙動設定(config 由来。EngineServer.ApplyConfig が更新する)。
public sealed class SessionSettings
{
    // サジェスト(入力中予測)を出すか。use_history/dictionary/realtime のいずれか有効で true。
    public bool SuggestionEnabled = true;
    // サジェスト最大件数(config.suggestions_size)。
    public int SuggestionSize = 9;
    // シークレットモード。履歴学習と履歴由来サジェストを抑止する。
    public bool IncognitoMode = false;
    // InsertSpace コマンドが挿入する空白の字形(config.space_character_form 由来)。
    public SpaceForm SpaceForm = SpaceForm.Half;
    // 候補選択ショートカット文字列(config.selection_shortcut。"123456789"/"asdfghjkl"/空)。
    public string SelectionShortcuts = string.Empty;
    // 個別のサジェストソース可否(history/dictionary/realtime)。Session が選択的に絞る。
    public bool UseHistorySuggest = true;
    public bool UseDictionarySuggest = true;
    public bool UseRealtimeConversion = true;
}

// スペース字形(InsertSpace コマンド用)。Default は半角扱い。
public enum SpaceForm { Half, Full }

// C++ SessionCommand.CommandType の主要部(候補選択/確定/取消)。
public enum SessionCommandType
{
    None,
    Revert,
    Submit,
    SelectCandidate,
    HighlightCandidate,
    SubmitCandidate,
    Undo,
}

public sealed class Input
{
    public CommandType Type { get; init; }
    public ulong SessionId { get; init; }
    public KeyEvent? Key { get; init; }
    public string KeyString { get; init; } = string.Empty;
    // SEND_COMMAND 用。
    public SessionCommandType SessionCommand { get; init; } = SessionCommandType.None;
    public int CommandId { get; init; }
    // SET_CONFIG 用の protobuf Config バイト列。
    public byte[] ConfigBytes { get; init; } = global::System.Array.Empty<byte>();
    // このリクエストでサジェストを抑止するか(commands.proto context.suppress_suggestion /
    // request_suggestion=false。パスワード欄等でクライアントが中間サジェストを止めたいとき)。
    public bool SuppressSuggestion { get; init; }
    // パスワード入力欄(context.input_field_type=PASSWORD)か。サジェスト抑止に加え、
    // 確定テキストを共有ユーザー履歴へ学習させない(後続候補窓へ秘密が漏れるのを防ぐ)。
    public bool IsPasswordField { get; init; }
}

public sealed class Output
{
    public ulong SessionId { get; init; }
    public bool Consumed { get; init; }
    public string Preedit { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty; // 確定文字列(あれば)
    public IReadOnlyList<string> Candidates { get; init; } = global::System.Array.Empty<string>();
    // 各候補の説明(記号/全角 等。Candidates と同数か空)。
    public IReadOnlyList<string> CandidateDescriptions { get; init; } = global::System.Array.Empty<string>();
    // 入力中サジェスト(変換前の予測候補)。
    public IReadOnlyList<string> Suggestions { get; init; } = global::System.Array.Empty<string>();
    // 変換候補ウィンドウで選択中の候補インデックス(-1=未注目/サジェスト)。
    public int FocusedIndex { get; init; } = -1;
    // 注目文節が preedit 上で始まる文字位置(候補ウィンドウのアンカー)。
    public int FocusedPosition { get; init; }
    public bool ErrorOccured { get; init; }
    // GET_CONFIG の応答 protobuf Config バイト列。
    public byte[] ConfigBytes { get; init; } = global::System.Array.Empty<byte>();
    // 確定したコマンド候補のコマンド(EngineServer が incognito/presentation を実行する)。
    public Mozc.Converter.Candidate.CommandType ConverterCommand { get; init; }
        = Mozc.Converter.Candidate.CommandType.DefaultCommand;
    // IME 有効状態(Status.activated として返す。false=直接入力)。
    public bool Activated { get; init; } = true;
}
