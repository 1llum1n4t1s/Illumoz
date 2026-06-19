using System.Buffers.Binary;
using System.Text;

namespace Mozc.Prediction;

// C++ src/prediction/user_history_predictor.cc の保存/読み込み(user_history.db 相当)。
// UserHistoryPredictor の Snapshot/Restore を介してエントリをファイルへ決定的に
// 直列化する。形式: [u32 magic][u32 count] 各 {[u32 keyLen][key][u32 valLen][val]
// [i32 freq][i64 lastAccess]}。文字列は UTF-8。
public static class UserHistoryStorage
{
    private const uint Magic = 0x4D5A4855; // "MZHU"

    public static byte[] Serialize(UserHistoryPredictor predictor)
    {
        var entries = new List<UserHistoryPredictor.Entry>(predictor.Snapshot());
        // 決定性: (Key, Value) の ordinal で整列。
        entries.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.Key, b.Key);
            return c != 0 ? c : string.CompareOrdinal(a.Value, b.Value);
        });

        using var ms = new global::System.IO.MemoryStream();
        WriteU32(ms, Magic);
        WriteU32(ms, entries.Count);
        foreach (UserHistoryPredictor.Entry e in entries)
        {
            WriteStr(ms, e.Key);
            WriteStr(ms, e.Value);
            WriteI32(ms, e.Frequency);
            WriteI64(ms, e.LastAccess);
        }
        return ms.ToArray();
    }

    public static void Save(UserHistoryPredictor predictor, string path)
        => global::System.IO.File.WriteAllBytes(path, Serialize(predictor));

    // data を predictor に流し込む(既存に追記する Restore を使用)。失敗時は false。
    public static bool Load(UserHistoryPredictor predictor, ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(data) != Magic)
        {
            return false;
        }
        try
        {
            int pos = 4;
            int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
            pos += 4;
            // 巨大 count による過大アロケーション(破損/悪意ファイル)を弾く。
            // 最小エントリ = 空文字2つ(各4B)+ freq(4B)+ lastAccess(8B)= 20B。
            const int MinEntrySize = 4 + 4 + 4 + 8;
            if (count < 0 || count > (data.Length - pos) / MinEntrySize)
            {
                return false;
            }
            var entries = new List<UserHistoryPredictor.Entry>(count);
            for (int i = 0; i < count; i++)
            {
                string key = ReadStr(data, ref pos);
                string value = ReadStr(data, ref pos);
                int freq = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(pos, 4));
                pos += 4;
                long last = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(pos, 8));
                pos += 8;
                entries.Add(new UserHistoryPredictor.Entry
                {
                    Key = key,
                    Value = value,
                    Frequency = freq,
                    LastAccess = last,
                });
            }
            predictor.Restore(entries);
            return true;
        }
        catch (global::System.Exception)
        {
            // 破損ファイル(境界外/長さ不正)で落とさず空履歴として継続。
            return false;
        }
    }

    public static bool LoadFile(UserHistoryPredictor predictor, string path)
        => global::System.IO.File.Exists(path)
            && Load(predictor, global::System.IO.File.ReadAllBytes(path));

    private static void WriteU32(global::System.IO.MemoryStream ms, uint v)
    {
        global::System.Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        ms.Write(b);
    }

    private static void WriteU32(global::System.IO.MemoryStream ms, int v) => WriteU32(ms, (uint)v);

    private static void WriteI32(global::System.IO.MemoryStream ms, int v)
    {
        global::System.Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, v);
        ms.Write(b);
    }

    private static void WriteI64(global::System.IO.MemoryStream ms, long v)
    {
        global::System.Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, v);
        ms.Write(b);
    }

    private static void WriteStr(global::System.IO.MemoryStream ms, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        WriteU32(ms, b.Length);
        ms.Write(b);
    }

    private static string ReadStr(ReadOnlySpan<byte> data, ref int pos)
    {
        int len = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
        pos += 4;
        string s = Encoding.UTF8.GetString(data.Slice(pos, len));
        pos += len;
        return s;
    }
}
