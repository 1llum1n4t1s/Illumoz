using Google.Protobuf;
using Mozc.Session;
using Pb = Mozc.Commands;

namespace Mozc.Server;

// commands.proto(protoc 生成・C++ ワイヤー互換)と内部 POCO(Mozc.Session.Input/Output)の橋渡し。
// IPC ペイロードを C++ mozc_server/client と相互運用可能な protobuf バイト列にする。
// CommandCodec(独自バイナリ)の置き換え候補。Preedit/Config 等の完全マッピングは後続。
public static class ProtoBridge
{
    public static Input DecodeInput(byte[] data)
    {
        Pb.Input proto = Pb.Input.Parser.ParseFrom(data);
        CommandType type = proto.Type switch
        {
            Pb.Input.Types.CommandType.CreateSession => CommandType.CreateSession,
            Pb.Input.Types.CommandType.DeleteSession => CommandType.DeleteSession,
            Pb.Input.Types.CommandType.SendKey => CommandType.SendKey,
            // TEST_SEND_KEY を NoOperation に潰さない(IME のキー横取り判定が常に未消費に
            // なる不具合の修正)。状態を変えずに消費可否だけ返す。
            Pb.Input.Types.CommandType.TestSendKey => CommandType.TestSendKey,
            Pb.Input.Types.CommandType.SendCommand => CommandType.SendCommand,
            Pb.Input.Types.CommandType.GetConfig => CommandType.GetConfig,
            Pb.Input.Types.CommandType.SetConfig => CommandType.SetConfig,
            // 保守コマンドを no-op に潰さず本来の処理へ回す(履歴クリア等が黙って成功扱い
            // になる不具合の修正)。
            Pb.Input.Types.CommandType.ClearUserHistory => CommandType.ClearUserHistory,
            Pb.Input.Types.CommandType.ClearUserPrediction => CommandType.ClearUserPrediction,
            Pb.Input.Types.CommandType.ClearUnusedUserPrediction => CommandType.ClearUnusedUserPrediction,
            Pb.Input.Types.CommandType.Reload => CommandType.Reload,
            Pb.Input.Types.CommandType.SyncData => CommandType.SyncData,
            _ => CommandType.NoOperation,
        };
        // SET_CONFIG の Config を内部 Input へ引き渡す(設定が反映されない不具合の修正)。
        byte[] configBytes = proto.Config != null
            ? proto.Config.ToByteArray()
            : global::System.Array.Empty<byte>();
        KeyEvent? key = proto.Key != null ? DecodeKey(proto.Key) : null;

        var sessionCommand = SessionCommandType.None;
        int commandId = 0;
        if (proto.Command != null)
        {
            sessionCommand = proto.Command.Type switch
            {
                Pb.SessionCommand.Types.CommandType.Revert => SessionCommandType.Revert,
                Pb.SessionCommand.Types.CommandType.Submit => SessionCommandType.Submit,
                Pb.SessionCommand.Types.CommandType.SelectCandidate => SessionCommandType.SelectCandidate,
                Pb.SessionCommand.Types.CommandType.HighlightCandidate => SessionCommandType.HighlightCandidate,
                Pb.SessionCommand.Types.CommandType.SubmitCandidate => SessionCommandType.SubmitCandidate,
                // ツールバー/クライアント駆動の UNDO(protobuf SEND_COMMAND)を直前確定の復元へ回す。
                // None に潰すと未消費になり、キーボード以外からの取り消しが効かない。
                Pb.SessionCommand.Types.CommandType.Undo => SessionCommandType.Undo,
                _ => SessionCommandType.None,
            };
            commandId = proto.Command.Id;
        }

        // パスワード欄(context.input_field_type=PASSWORD)はサジェスト/履歴学習を止める対象。
        // この型情報を落とすと、クライアントが suppress_suggestion を併送しない限り中間サジェストが
        // 出て確定テキストが共有履歴に学習され、後続の候補窓へ秘密が漏れる。
        bool isPassword = proto.Context != null
            && proto.Context.InputFieldType == Pb.Context.Types.InputFieldType.Password;

        return new Input
        {
            Type = type,
            SessionId = proto.Id,
            Key = key,
            // key_code が無く key_string のみのソフトキーボード/かな入力を取りこぼさない。
            KeyString = proto.Key != null && proto.Key.HasKeyString ? proto.Key.KeyString : string.Empty,
            SessionCommand = sessionCommand,
            CommandId = commandId,
            ConfigBytes = configBytes,
            // context.suppress_suggestion=true、request_suggestion=false、または PASSWORD 欄を抑止扱い。
            SuppressSuggestion = isPassword
                || (proto.Context != null && proto.Context.SuppressSuggestion)
                || (proto.HasRequestSuggestion && !proto.RequestSuggestion),
            IsPasswordField = isPassword,
        };
    }

    public static byte[] EncodeOutput(Output output) => EncodeOutput(output, string.Empty);

    // shortcuts が指定されれば候補 i に annotation.shortcut = shortcuts[i] を付与する
    // (C++ SelectionShortcut: "123456789" / "asdfghjkl")。
    // preeditOverride が指定されれば表示 preedit をそれで差し替える(文字形ルール適用後の文字列)。
    public static byte[] EncodeOutput(Output output, string shortcuts, string? preeditOverride = null)
    {
        string preeditText = preeditOverride ?? output.Preedit;
        var proto = new Pb.Output
        {
            Id = output.SessionId,
            Consumed = output.Consumed,
            // IME 有効/直接入力状態をクライアントへ返す(モード表示・キー素通し判定に使う)。
            Status = new Pb.Status { Activated = output.Activated },
        };
        // 失敗した EvalCommand は error_code=SESSION_FAILURE を返す(commands.proto)。
        // 既定の成功値のままだとクライアントが不正セッションを成功と誤認する。
        if (output.ErrorOccured)
        {
            proto.ErrorCode = Pb.Output.Types.ErrorCode.SessionFailure;
        }
        // GET_CONFIG/SET_CONFIG の応答 Config を protobuf に書き戻す(設定 UI が既定値を
        // 読んでしまう不具合の修正)。
        if (output.ConfigBytes.Length != 0)
        {
            proto.Config = Mozc.Config.Config.Parser.ParseFrom(output.ConfigBytes);
        }
        if (preeditText.Length != 0)
        {
            int len = Mozc.Base.GraphemeSplitter.Split(preeditText).Count;
            var preedit = new Pb.Preedit { Cursor = (uint)len };
            preedit.Segment.Add(new Pb.Preedit.Types.Segment
            {
                Annotation = Pb.Preedit.Types.Segment.Types.Annotation.Underline,
                Value = preeditText,
                ValueLength = (uint)len,
            });
            proto.Preedit = preedit;
        }
        if (output.Result.Length != 0)
        {
            proto.Result = new Pb.Result
            {
                Type = Pb.Result.Types.ResultType.String,
                Value = output.Result,
            };
        }
        if (output.Candidates.Count > 0)
        {
            var cw = new Pb.CandidateWindow { Size = (uint)output.Candidates.Count };
            // position は candidate_window.proto の proto2 required フィールド。C++ engine_output.cc は
            // focused の有無に依らず常に set_position するため、ここでも無条件に設定する
            // (proto2 クライアントが required 欠落で Output を uninitialized 拒否するのを防ぐ)。
            cw.Position = (uint)output.FocusedPosition;
            // 選択中インデックス(focused_index)は注目候補があるときだけ設定する。
            if (output.FocusedIndex >= 0)
            {
                cw.FocusedIndex = (uint)output.FocusedIndex;
            }
            for (int i = 0; i < output.Candidates.Count; i++)
            {
                var cand = new Pb.CandidateWindow.Types.Candidate
                {
                    // Id は native クライアント(IBus/mac renderer)が選択時に送り返す候補識別子。
                    // 現状 managed の候補列はフラット(cascading/paging 無し)なので Id==Index==配列添字。
                    // この不変条件が崩れる(サブ候補/ページング導入)なら受信側で id→index 解決が必要。
                    Id = i,
                    Index = (uint)i,
                    Value = output.Candidates[i],
                };
                if (i < shortcuts.Length)
                {
                    cand.Annotation = new Pb.Annotation { Shortcut = shortcuts[i].ToString() };
                }
                if (i < output.CandidateDescriptions.Count && output.CandidateDescriptions[i].Length > 0)
                {
                    cand.Annotation ??= new Pb.Annotation();
                    cand.Annotation.Description = output.CandidateDescriptions[i];
                }
                cw.Candidate.Add(cand);
            }
            proto.CandidateWindow = cw;
        }
        else if (output.Suggestions.Count > 0)
        {
            // 変換候補が無く入力中サジェストがある場合は SUGGESTION として候補窓に載せる。
            var cw = new Pb.CandidateWindow
            {
                Size = (uint)output.Suggestions.Count,
                Category = Pb.Category.Suggestion,
                // position は proto2 required フィールド。サジェスト(非 Conversion)では 0 だが、
                // 明示設定して presence を立てないと proto2 クライアントが Output を拒否しうる。
                Position = (uint)output.FocusedPosition,
            };
            for (int i = 0; i < output.Suggestions.Count; i++)
            {
                var cand = new Pb.CandidateWindow.Types.Candidate
                {
                    // native 選択用の候補 id(フラットな候補列なので Id==Index==配列添字)。
                    Id = i,
                    Index = (uint)i,
                    Value = output.Suggestions[i],
                };
                if (i < shortcuts.Length)
                {
                    cand.Annotation = new Pb.Annotation { Shortcut = shortcuts[i].ToString() };
                }
                cw.Candidate.Add(cand);
            }
            proto.CandidateWindow = cw;
        }
        return proto.ToByteArray();
    }

    private static KeyEvent DecodeKey(Pb.KeyEvent proto)
    {
        var ke = new KeyEvent();
        if (proto.HasKeyCode)
        {
            ke.KeyCode = (int)proto.KeyCode;
        }
        // クライアントが IME 有効状態を明示していれば取り込む(間接 IME off 処理)。
        // activated=false の印字キーは session 側で素通しさせる。
        if (proto.HasActivated)
        {
            ke.Activated = proto.Activated;
        }
        // input_style を取り込む。DIRECT_INPUT の key_string は precomposition で即時確定する
        // (ソフトキーボード/クライアント供給の直接テキスト)。未指定は FollowMode(従来パス)。
        if (proto.HasInputStyle)
        {
            ke.InputStyle = proto.InputStyle switch
            {
                Pb.KeyEvent.Types.InputStyle.AsIs => InputStyle.AsIs,
                Pb.KeyEvent.Types.InputStyle.DirectInput => InputStyle.DirectInput,
                _ => InputStyle.FollowMode,
            };
        }
        // NO_SPECIALKEY(enum 既定値 0)を明示シリアライズしてきたクライアントでは HasSpecialKey が
        // true になる。これを特殊キーとして Special=UndefinedKey にすると、印字キー(key_code/
        // key_string)が特殊キー扱いになり通常文字を入力できなくなる。NO_SPECIALKEY は「特殊キー無し」
        // を意味するので Special は未設定のままにする。
        if (proto.HasSpecialKey
            && proto.SpecialKey != Pb.KeyEvent.Types.SpecialKey.NoSpecialkey)
        {
            // テンキーの数字(NUMPAD0-9)は keymap に行が無く、印字フォールバックも
            // Special!=null だと効かない。'0'-'9' の key_code に正規化して通常入力扱いにする。
            if (proto.SpecialKey >= Pb.KeyEvent.Types.SpecialKey.Numpad0
                && proto.SpecialKey <= Pb.KeyEvent.Types.SpecialKey.Numpad9)
            {
                ke.KeyCode = '0' + (proto.SpecialKey - Pb.KeyEvent.Types.SpecialKey.Numpad0);
            }
            else
            {
                ke.Special = MapSpecial(proto.SpecialKey);
            }
        }
        foreach (Pb.KeyEvent.Types.ModifierKey m in proto.ModifierKeys)
        {
            // Windows/Mac クライアントは LEFT_CTRL/RIGHT_CTRL 等の左右別修飾を送ることがある。
            // keymap 照合は基本修飾(Ctrl/Alt/Shift)で行うため、左右変種を基本修飾へ畳む。
            switch (m)
            {
                case Pb.KeyEvent.Types.ModifierKey.Ctrl:
                case Pb.KeyEvent.Types.ModifierKey.LeftCtrl:
                case Pb.KeyEvent.Types.ModifierKey.RightCtrl:
                    ke.Modifiers.Add(ModifierKey.Ctrl);
                    break;
                case Pb.KeyEvent.Types.ModifierKey.Shift:
                case Pb.KeyEvent.Types.ModifierKey.LeftShift:
                case Pb.KeyEvent.Types.ModifierKey.RightShift:
                    ke.Modifiers.Add(ModifierKey.Shift);
                    break;
                case Pb.KeyEvent.Types.ModifierKey.Alt:
                case Pb.KeyEvent.Types.ModifierKey.LeftAlt:
                case Pb.KeyEvent.Types.ModifierKey.RightAlt:
                    ke.Modifiers.Add(ModifierKey.Alt);
                    break;
                case Pb.KeyEvent.Types.ModifierKey.Caps:
                    ke.Modifiers.Add(ModifierKey.Caps);
                    break;
            }
        }
        return ke;
    }

    // commands.proto の SpecialKey を内部 SpecialKey に対応付ける。
    // F6-F10 等の変換ショートカットや PageUp/PageDown の候補ページングが
    // protobuf 境界を越えて機能するよう、全特殊キーを網羅的にマップする。
    private static SpecialKey? MapSpecial(Pb.KeyEvent.Types.SpecialKey s) => s switch
    {
        Pb.KeyEvent.Types.SpecialKey.On => SpecialKey.On,
        Pb.KeyEvent.Types.SpecialKey.Off => SpecialKey.Off,
        Pb.KeyEvent.Types.SpecialKey.Space => SpecialKey.Space,
        Pb.KeyEvent.Types.SpecialKey.Enter => SpecialKey.Enter,
        Pb.KeyEvent.Types.SpecialKey.Backspace => SpecialKey.Backspace,
        Pb.KeyEvent.Types.SpecialKey.Escape => SpecialKey.Escape,
        Pb.KeyEvent.Types.SpecialKey.Del => SpecialKey.Del,
        Pb.KeyEvent.Types.SpecialKey.Insert => SpecialKey.Insert,
        Pb.KeyEvent.Types.SpecialKey.Tab => SpecialKey.Tab,
        Pb.KeyEvent.Types.SpecialKey.Left => SpecialKey.Left,
        Pb.KeyEvent.Types.SpecialKey.Right => SpecialKey.Right,
        Pb.KeyEvent.Types.SpecialKey.Up => SpecialKey.Up,
        Pb.KeyEvent.Types.SpecialKey.Down => SpecialKey.Down,
        Pb.KeyEvent.Types.SpecialKey.Home => SpecialKey.Home,
        Pb.KeyEvent.Types.SpecialKey.End => SpecialKey.End,
        Pb.KeyEvent.Types.SpecialKey.PageUp => SpecialKey.PageUp,
        Pb.KeyEvent.Types.SpecialKey.PageDown => SpecialKey.PageDown,
        Pb.KeyEvent.Types.SpecialKey.Henkan => SpecialKey.Henkan,
        Pb.KeyEvent.Types.SpecialKey.Muhenkan => SpecialKey.Muhenkan,
        Pb.KeyEvent.Types.SpecialKey.Kana => SpecialKey.Kana,
        Pb.KeyEvent.Types.SpecialKey.Katakana => SpecialKey.Katakana,
        Pb.KeyEvent.Types.SpecialKey.Eisu => SpecialKey.Eisu,
        Pb.KeyEvent.Types.SpecialKey.Hankaku => SpecialKey.Hankaku,
        Pb.KeyEvent.Types.SpecialKey.Kanji => SpecialKey.Kanji,
        Pb.KeyEvent.Types.SpecialKey.TextInput => SpecialKey.TextInput,
        Pb.KeyEvent.Types.SpecialKey.F1 => SpecialKey.F1,
        Pb.KeyEvent.Types.SpecialKey.F2 => SpecialKey.F2,
        Pb.KeyEvent.Types.SpecialKey.F3 => SpecialKey.F3,
        Pb.KeyEvent.Types.SpecialKey.F4 => SpecialKey.F4,
        Pb.KeyEvent.Types.SpecialKey.F5 => SpecialKey.F5,
        Pb.KeyEvent.Types.SpecialKey.F6 => SpecialKey.F6,
        Pb.KeyEvent.Types.SpecialKey.F7 => SpecialKey.F7,
        Pb.KeyEvent.Types.SpecialKey.F8 => SpecialKey.F8,
        Pb.KeyEvent.Types.SpecialKey.F9 => SpecialKey.F9,
        Pb.KeyEvent.Types.SpecialKey.F10 => SpecialKey.F10,
        Pb.KeyEvent.Types.SpecialKey.F11 => SpecialKey.F11,
        Pb.KeyEvent.Types.SpecialKey.F12 => SpecialKey.F12,
        Pb.KeyEvent.Types.SpecialKey.F13 => SpecialKey.F13,
        Pb.KeyEvent.Types.SpecialKey.F14 => SpecialKey.F14,
        Pb.KeyEvent.Types.SpecialKey.F15 => SpecialKey.F15,
        Pb.KeyEvent.Types.SpecialKey.F16 => SpecialKey.F16,
        Pb.KeyEvent.Types.SpecialKey.F17 => SpecialKey.F17,
        Pb.KeyEvent.Types.SpecialKey.F18 => SpecialKey.F18,
        Pb.KeyEvent.Types.SpecialKey.F19 => SpecialKey.F19,
        Pb.KeyEvent.Types.SpecialKey.F20 => SpecialKey.F20,
        Pb.KeyEvent.Types.SpecialKey.F21 => SpecialKey.F21,
        Pb.KeyEvent.Types.SpecialKey.F22 => SpecialKey.F22,
        Pb.KeyEvent.Types.SpecialKey.F23 => SpecialKey.F23,
        Pb.KeyEvent.Types.SpecialKey.F24 => SpecialKey.F24,
        Pb.KeyEvent.Types.SpecialKey.Numpad0 => SpecialKey.Numpad0,
        Pb.KeyEvent.Types.SpecialKey.Numpad1 => SpecialKey.Numpad1,
        Pb.KeyEvent.Types.SpecialKey.Numpad2 => SpecialKey.Numpad2,
        Pb.KeyEvent.Types.SpecialKey.Numpad3 => SpecialKey.Numpad3,
        Pb.KeyEvent.Types.SpecialKey.Numpad4 => SpecialKey.Numpad4,
        Pb.KeyEvent.Types.SpecialKey.Numpad5 => SpecialKey.Numpad5,
        Pb.KeyEvent.Types.SpecialKey.Numpad6 => SpecialKey.Numpad6,
        Pb.KeyEvent.Types.SpecialKey.Numpad7 => SpecialKey.Numpad7,
        Pb.KeyEvent.Types.SpecialKey.Numpad8 => SpecialKey.Numpad8,
        Pb.KeyEvent.Types.SpecialKey.Numpad9 => SpecialKey.Numpad9,
        Pb.KeyEvent.Types.SpecialKey.Multiply => SpecialKey.Multiply,
        Pb.KeyEvent.Types.SpecialKey.Add => SpecialKey.Add,
        Pb.KeyEvent.Types.SpecialKey.Separator => SpecialKey.Separator,
        Pb.KeyEvent.Types.SpecialKey.Subtract => SpecialKey.Subtract,
        Pb.KeyEvent.Types.SpecialKey.Decimal => SpecialKey.Decimal,
        Pb.KeyEvent.Types.SpecialKey.Divide => SpecialKey.Divide,
        Pb.KeyEvent.Types.SpecialKey.Equals => SpecialKey.Equals,
        Pb.KeyEvent.Types.SpecialKey.Comma => SpecialKey.Comma,
        Pb.KeyEvent.Types.SpecialKey.Clear => SpecialKey.Clear,
        // 仮想キー(モバイル/ソフトキーボードが直接送る。keymap の VirtualLeft 等と一致させる)。
        Pb.KeyEvent.Types.SpecialKey.VirtualLeft => SpecialKey.VirtualLeft,
        Pb.KeyEvent.Types.SpecialKey.VirtualRight => SpecialKey.VirtualRight,
        Pb.KeyEvent.Types.SpecialKey.VirtualEnter => SpecialKey.VirtualEnter,
        Pb.KeyEvent.Types.SpecialKey.VirtualUp => SpecialKey.VirtualUp,
        Pb.KeyEvent.Types.SpecialKey.VirtualDown => SpecialKey.VirtualDown,
        _ => SpecialKey.UndefinedKey,
    };
}
