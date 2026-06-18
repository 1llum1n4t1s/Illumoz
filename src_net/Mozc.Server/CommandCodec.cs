using System.Text;
using Mozc.Session;

namespace Mozc.Server;

// Input/Output の IPC ペイロード(byte[])シリアライザ。
// 注: 現状は独自バイナリ形式。C++ とのワイヤー互換(commands.proto のシリアライズ)は
// Mozc.Protocol の protobuf 生成完了後に差し替える(C7/IPC 互換タスク)。
public static class CommandCodec
{
    public static byte[] EncodeInput(Input input)
    {
        using var ms = new global::System.IO.MemoryStream();
        using var w = new global::System.IO.BinaryWriter(ms, Encoding.UTF8);
        w.Write((byte)input.Type);
        w.Write(input.SessionId);
        WriteString(w, input.KeyString);
        return ms.ToArray();
    }

    public static Input DecodeInput(byte[] data)
    {
        using var ms = new global::System.IO.MemoryStream(data);
        using var r = new global::System.IO.BinaryReader(ms, Encoding.UTF8);
        var type = (CommandType)r.ReadByte();
        ulong sessionId = r.ReadUInt64();
        string keyString = ReadString(r);
        return new Input { Type = type, SessionId = sessionId, KeyString = keyString };
    }

    public static byte[] EncodeOutput(Output output)
    {
        using var ms = new global::System.IO.MemoryStream();
        using var w = new global::System.IO.BinaryWriter(ms, Encoding.UTF8);
        w.Write(output.SessionId);
        w.Write(output.Consumed);
        w.Write(output.ErrorOccured);
        WriteString(w, output.Preedit);
        WriteString(w, output.Result);
        w.Write(output.Candidates.Count);
        foreach (string c in output.Candidates)
        {
            WriteString(w, c);
        }
        return ms.ToArray();
    }

    public static Output DecodeOutput(byte[] data)
    {
        using var ms = new global::System.IO.MemoryStream(data);
        using var r = new global::System.IO.BinaryReader(ms, Encoding.UTF8);
        ulong sessionId = r.ReadUInt64();
        bool consumed = r.ReadBoolean();
        bool error = r.ReadBoolean();
        string preedit = ReadString(r);
        string result = ReadString(r);
        int n = r.ReadInt32();
        var candidates = new List<string>(n);
        for (int i = 0; i < n; i++)
        {
            candidates.Add(ReadString(r));
        }
        return new Output
        {
            SessionId = sessionId,
            Consumed = consumed,
            ErrorOccured = error,
            Preedit = preedit,
            Result = result,
            Candidates = candidates,
        };
    }

    private static void WriteString(global::System.IO.BinaryWriter w, string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    private static string ReadString(global::System.IO.BinaryReader r)
    {
        int len = r.ReadInt32();
        return Encoding.UTF8.GetString(r.ReadBytes(len));
    }
}
