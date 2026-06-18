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
            _ => CommandType.NoOperation,
        };
        KeyEvent? key = proto.Key != null ? DecodeKey(proto.Key) : null;
        return new Input { Type = type, SessionId = proto.Id, Key = key };
    }

    public static byte[] EncodeOutput(Output output)
    {
        var proto = new Pb.Output
        {
            Id = output.SessionId,
            Consumed = output.Consumed,
        };
        if (output.Preedit.Length != 0)
        {
            int len = new global::System.Globalization.StringInfo(output.Preedit).LengthInTextElements;
            var preedit = new Pb.Preedit { Cursor = (uint)len };
            preedit.Segment.Add(new Pb.Preedit.Types.Segment
            {
                Annotation = Pb.Preedit.Types.Segment.Types.Annotation.Underline,
                Value = output.Preedit,
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
            for (int i = 0; i < output.Candidates.Count; i++)
            {
                cw.Candidate.Add(new Pb.CandidateWindow.Types.Candidate
                {
                    Index = (uint)i,
                    Value = output.Candidates[i],
                });
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
        if (proto.HasSpecialKey)
        {
            ke.Special = MapSpecial(proto.SpecialKey);
        }
        foreach (Pb.KeyEvent.Types.ModifierKey m in proto.ModifierKeys)
        {
            switch (m)
            {
                case Pb.KeyEvent.Types.ModifierKey.Ctrl:
                    ke.Modifiers.Add(ModifierKey.Ctrl);
                    break;
                case Pb.KeyEvent.Types.ModifierKey.Shift:
                    ke.Modifiers.Add(ModifierKey.Shift);
                    break;
                case Pb.KeyEvent.Types.ModifierKey.Alt:
                    ke.Modifiers.Add(ModifierKey.Alt);
                    break;
                case Pb.KeyEvent.Types.ModifierKey.Caps:
                    ke.Modifiers.Add(ModifierKey.Caps);
                    break;
            }
        }
        return ke;
    }

    private static SpecialKey? MapSpecial(Pb.KeyEvent.Types.SpecialKey s) => s switch
    {
        Pb.KeyEvent.Types.SpecialKey.Space => SpecialKey.Space,
        Pb.KeyEvent.Types.SpecialKey.Enter => SpecialKey.Enter,
        Pb.KeyEvent.Types.SpecialKey.Backspace => SpecialKey.Backspace,
        Pb.KeyEvent.Types.SpecialKey.Escape => SpecialKey.Escape,
        Pb.KeyEvent.Types.SpecialKey.Del => SpecialKey.Del,
        Pb.KeyEvent.Types.SpecialKey.Tab => SpecialKey.Tab,
        Pb.KeyEvent.Types.SpecialKey.Left => SpecialKey.Left,
        Pb.KeyEvent.Types.SpecialKey.Right => SpecialKey.Right,
        Pb.KeyEvent.Types.SpecialKey.Up => SpecialKey.Up,
        Pb.KeyEvent.Types.SpecialKey.Down => SpecialKey.Down,
        Pb.KeyEvent.Types.SpecialKey.Home => SpecialKey.Home,
        Pb.KeyEvent.Types.SpecialKey.End => SpecialKey.End,
        _ => SpecialKey.UndefinedKey,
    };
}
