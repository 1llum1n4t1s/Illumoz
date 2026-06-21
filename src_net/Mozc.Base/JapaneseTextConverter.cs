using System.Text;

namespace Mozc.Base;

// C++ base/strings/internal/double_array.cc の ConvertUsingDoubleArray 忠実移植。
// UTF-8 バイト列上で double-array トライを引き、部分文字列を順次置換する。
internal static class JapaneseTextConverter
{
    private static int OneCharLen(byte lead)
    {
        if (lead < 0x80) return 1;
        if ((lead & 0xE0) == 0xC0) return 2;
        if ((lead & 0xF0) == 0xE0) return 3;
        if ((lead & 0xF8) == 0xF0) return 4;
        return 1;
    }

    // (seekto, index) を返す。seekto==0 は不一致。
    private static (int Seekto, int Index) Lookup(
        (short Base, ushort Check)[] array, byte[] key, int start)
    {
        if (array.Length == 0)
        {
            return (0, 0);
        }
        int b = array[0].Base;
        int seekto = 0;
        int index = 0;
        for (int i = start; i < key.Length; i++)
        {
            int p = b;
            if (p < 0 || p >= array.Length)
            {
                return (seekto, index);
            }
            int n = array[p].Base;
            if ((uint)b == array[p].Check && n < 0)
            {
                seekto = i - start;
                index = -n - 1;
            }
            p = b + key[i] + 1;
            if (p >= 0 && p < array.Length && (uint)b == array[p].Check)
            {
                b = array[p].Base;
            }
            else
            {
                return (seekto, index);
            }
        }
        {
            int p = b;
            if (p >= 0 && p < array.Length)
            {
                int n = array[p].Base;
                if ((uint)b == array[p].Check && n < 0)
                {
                    seekto = key.Length - start;
                    index = -n - 1;
                }
            }
        }
        return (seekto, index);
    }

    public static string Convert((short Base, ushort Check)[] da, byte[] ctable, string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        var output = new List<byte>(bytes.Length);
        int i = 0;
        while (i < bytes.Length)
        {
            (int seekto, int index) = Lookup(da, bytes, i);
            if (seekto > 0)
            {
                // ctable[index] から null 終端までが出力文字列、その次のバイトが巻き戻し量。
                int end = index;
                while (ctable[end] != 0)
                {
                    end++;
                }
                int slen = end - index;
                for (int k = index; k < end; k++)
                {
                    output.Add(ctable[k]);
                }
                int rewind = ctable[index + slen + 1];
                int mblen = seekto - rewind;
                i += mblen;
            }
            else
            {
                int mblen = OneCharLen(bytes[i]);
                for (int k = 0; k < mblen && i + k < bytes.Length; k++)
                {
                    output.Add(bytes[i + k]);
                }
                i += mblen;
            }
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
