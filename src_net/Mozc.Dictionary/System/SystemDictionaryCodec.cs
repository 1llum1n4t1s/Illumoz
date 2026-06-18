using System.Text;

namespace Mozc.Dictionary.System;

// C++ src/dictionary/system/codec.cc 相当(値エンコード部)。
// 値文字列(UTF-8)をひらがな/カタカナ/漢字/ASCII などに最適化した可変長バイト列へ。
// キー/トークンのエンコードは別途移植する。
public sealed class SystemDictionaryCodec
{
    private const int KanjiOffset = 0x01;
    private const int HiraganaOffset = 0x4b;
    private const int KatakanaOffset = 0x9f;
    private const byte MarkAscii = 0xfc;
    private const byte MarkXX00 = 0xfd;
    private const byte MarkOtherUcs2 = 0xfe;
    private const byte MarkCodepoint = 0xff;
    private const int MarkCodepointMiddle0 = 0x80;
    private const int MarkCodepointRight0 = 0x40;
    private const int MarkCodepointLeftMask = 0x1f;

    // キー codec(codec.cc EncodeDecodeKeyImpl)。エンコード=デコードの対称変換。
    // 頻出ひらがな/カタカナを 1 バイト制御域へ写し trie キーを圧縮する。自己反転。
    public string EncodeKey(string key) => TransformKey(key);
    public string DecodeKey(string key) => TransformKey(key);

    public int GetEncodedKeyLength(string key) => Encoding.UTF8.GetByteCount(TransformKey(key));
    public int GetDecodedKeyLength(string key) => Encoding.UTF8.GetByteCount(TransformKey(key));

    private static string TransformKey(string src)
    {
        var sb = new StringBuilder(src.Length);
        foreach (Rune rune in src.EnumerateRunes())
        {
            uint code = (uint)rune.Value;
            int offset = 0;
            if ((code is >= 0x0001 and <= 0x001f) || (code is >= 0x3041 and <= 0x305f))
            {
                offset = 0x3041 - 0x0001;
            }
            else if ((code is >= 0x0040 and <= 0x0075) || (code is >= 0x3060 and <= 0x3095))
            {
                offset = 0x3060 - 0x0040;
            }
            else if ((code is >= 0x0076 and <= 0x0077) || (code is >= 0x30FB and <= 0x30FC))
            {
                offset = 0x30FB - 0x0076;
            }

            code = code < 0x80 ? (uint)(code + offset) : (uint)(code - offset);
            sb.Append(char.ConvertFromUtf32((int)code));
        }
        return sb.ToString();
    }

    public byte[] EncodeValue(string value)
    {
        var dst = new List<byte>(value.Length);
        foreach (Rune rune in value.EnumerateRunes())
        {
            uint c = (uint)rune.Value;
            if (c is >= 0x3041 and < 0x3095)
            {
                dst.Add((byte)(c - 0x3041 + HiraganaOffset));
            }
            else if (c is >= 0x30a1 and < 0x30fd)
            {
                dst.Add((byte)(c - 0x30a1 + KatakanaOffset));
            }
            else if (c < 0x10000 && ((c >> 8) & 255) == 0)
            {
                dst.Add(MarkAscii);
                dst.Add((byte)(c & 255));
            }
            else if (c < 0x10000 && (c & 255) == 0)
            {
                dst.Add(MarkXX00);
                dst.Add((byte)((c >> 8) & 255));
            }
            else if (c is >= 0x4e00 and < 0x9800)
            {
                dst.Add((byte)(((c - 0x4e00) >> 8) + KanjiOffset));
                dst.Add((byte)(c & 255));
            }
            else if (c is >= 0x10000 and <= 0x10ffff)
            {
                int left = (int)((c >> 16) & 255);
                int middle = (int)((c >> 8) & 255);
                int right = (int)(c & 255);
                if (middle == 0) left |= MarkCodepointMiddle0;
                if (right == 0) left |= MarkCodepointRight0;
                dst.Add(MarkCodepoint);
                dst.Add((byte)left);
                if (middle != 0) dst.Add((byte)middle);
                if (right != 0) dst.Add((byte)right);
            }
            else
            {
                dst.Add(MarkOtherUcs2);
                dst.Add((byte)((c >> 8) & 255));
                dst.Add((byte)(c & 255));
            }
        }
        return dst.ToArray();
    }

    public string DecodeValue(ReadOnlySpan<byte> src)
    {
        var sb = new StringBuilder();
        int p = 0;
        while (p < src.Length)
        {
            int cc = src[p];
            int c;
            if (HiraganaOffset <= cc && cc < KatakanaOffset)
            {
                c = 0x3041 + cc - HiraganaOffset;
                p += 1;
            }
            else if (KatakanaOffset <= cc && cc < MarkAscii)
            {
                c = 0x30a1 + cc - KatakanaOffset;
                p += 1;
            }
            else if (cc == MarkAscii)
            {
                c = src[p + 1];
                p += 2;
            }
            else if (cc == MarkXX00)
            {
                c = src[p + 1] << 8;
                p += 2;
            }
            else if (cc == MarkCodepoint)
            {
                c = (src[p + 1] & MarkCodepointLeftMask) << 16;
                int pos = p + 2;
                if ((src[p + 1] & MarkCodepointMiddle0) == 0)
                {
                    c += src[pos++] << 8;
                }
                if ((src[p + 1] & MarkCodepointRight0) == 0)
                {
                    c += src[pos++];
                }
                p = pos;
            }
            else if (cc == MarkOtherUcs2)
            {
                c = (src[p + 1] << 8) + src[p + 2];
                p += 3;
            }
            else if (cc < HiraganaOffset)
            {
                c = (((cc - KanjiOffset) << 8) + 0x4e00) + src[p + 1];
                p += 2;
            }
            else
            {
                throw new InvalidDataException($"invalid value codec byte 0x{cc:X2}");
            }
            sb.Append(char.ConvertFromUtf32(c));
        }
        return sb.ToString();
    }
}
