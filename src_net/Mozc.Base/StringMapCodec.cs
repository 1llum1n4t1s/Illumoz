using System.Buffers.Binary;
using System.Text;

namespace Mozc.Base;

// (文字列キー -> 文字列配列) の決定的バイナリ直列化(symbol/single_kanji/emoji 等の
// mozc.data セクション用)。形式: [u32 count] 各エントリ {[u32 keyLen][key utf8]
// [u32 valCount] 各値 {[u32 len][utf8]}}。キーは序列安定のため ordinal ソートする。
public static class StringMapCodec
{
    public static byte[] Serialize(IReadOnlyDictionary<string, string[]> map)
    {
        var keys = new List<string>(map.Keys);
        keys.Sort(global::System.StringComparer.Ordinal);

        using var ms = new global::System.IO.MemoryStream();
        WriteU32(ms, keys.Count);
        foreach (string k in keys)
        {
            WriteStr(ms, k);
            string[] vals = map[k];
            WriteU32(ms, vals.Length);
            foreach (string v in vals)
            {
                WriteStr(ms, v);
            }
        }
        return ms.ToArray();
    }

    private static void WriteU32(global::System.IO.MemoryStream ms, int v)
    {
        global::System.Span<byte> u32 = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)v);
        ms.Write(u32);
    }

    private static void WriteStr(global::System.IO.MemoryStream ms, string s)
    {
        byte[] b = Encoding.UTF8.GetBytes(s);
        WriteU32(ms, b.Length);
        ms.Write(b);
    }

    public static IReadOnlyDictionary<string, string[]> Deserialize(ReadOnlySpan<byte> data)
    {
        var dict = new Dictionary<string, string[]>();
        if (data.Length < 4)
        {
            return dict;
        }
        int pos = 0;
        int count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
        pos += 4;
        for (int i = 0; i < count; i++)
        {
            string key = ReadStr(data, ref pos);
            int vc = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
            pos += 4;
            var vals = new string[vc];
            for (int j = 0; j < vc; j++)
            {
                vals[j] = ReadStr(data, ref pos);
            }
            dict[key] = vals;
        }
        return dict;
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
