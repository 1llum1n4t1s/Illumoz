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

    // --- セクション名(codec.cc) ---
    public string SectionNameForKey => "k";
    public string SectionNameForValue => "v";
    public string SectionNameForTokens => "t";
    public string SectionNameForPos => "p";

    // --- トークン codec フラグ(codec.cc) ---
    private const byte SmallCostFlag = 0x80;
    private const byte SmallCostMask = 0x7f;
    public const byte TokensTerminationFlag = 0xff;
    private const byte ValueTypeFlagMask = 0x03;
    private const byte AsIsHiraganaValueFlag = 0x01;
    private const byte AsIsKatakanaValueFlag = 0x02;
    private const byte SameAsPrevValueFlag = 0x03;
    private const byte NormalValueFlag = 0x00;
    private const byte PosTypeFlagMask = 0x0c;
    private const byte FullPosFlag = 0x04;
    private const byte MonoPosFlag = 0x08;
    private const byte SameAsPrevPosFlag = 0x0c;
    private const byte FrequentPosFlag = 0x00;
    private const byte SpellingCorrectionFlag = 0x10;
    private const byte CrammedIdFlag = 0x40;
    private const byte UpperFlagsMask = 0xc0;
    private const byte UpperCrammedIdMask = 0x3f;
    private const byte LastTokenFlag = 0x80;

    // 検証用上限(codec.cc)。
    private const int PosMax = 0x0fff;       // 12bit
    private const int CostMax = 0x7fff;      // 15bit
    private const int ValueTrieIdMax = 0x3fffff; // 22bit

    public byte GetTokensTerminationFlag() => TokensTerminationFlag;

    // codec.cc EncodeTokens 相当。1 つのキーに紐づくトークン列を可変長バイト列へ。
    // 先頭バイトが終端フラグ(0xff)と衝突しないことは C++ 同様(最初のトークンは
    // SameAsPrev* にならない設計なので flags の下位ビットが立ち 0xff にはならない)。
    public byte[] EncodeTokens(IReadOnlyList<TokenInfo> tokens)
    {
        var output = new List<byte>();
        for (int i = 0; i < tokens.Count; i++)
        {
            EncodeToken(tokens, i, output);
        }
        if (output.Count > 0 && output[0] == TokensTerminationFlag)
        {
            throw new InvalidOperationException("first token byte must not equal the termination flag");
        }
        return output.ToArray();
    }

    private void EncodeToken(IReadOnlyList<TokenInfo> tokens, int index, List<byte> output)
    {
        byte flags = GetFlagsForToken(tokens, index);
        Span<byte> buff = stackalloc byte[9];
        buff[0] = flags;
        int offset = 1;
        TokenInfo info = tokens[index];
        EncodePos(info, flags, buff, ref offset);
        EncodeCost(info, buff, ref offset);
        EncodeValueInfo(info, flags, buff, ref offset);
        for (int i = 0; i < offset; i++)
        {
            output.Add(buff[i]);
        }
    }

    private static byte GetFlagForPos(TokenInfo info)
    {
        Token token = info.Token;
        if (token.Lid > PosMax || token.Rid > PosMax)
        {
            throw new InvalidOperationException($"too large pos id: lid {token.Lid}, rid {token.Rid}");
        }
        if (info.Pos == TokenInfo.PosType.FrequentPos)
        {
            return FrequentPosFlag;
        }
        if (info.Pos == TokenInfo.PosType.SameAsPrevPos)
        {
            return SameAsPrevPosFlag;
        }
        return token.Lid == token.Rid ? MonoPosFlag : FullPosFlag;
    }

    private static byte GetFlagForValue(TokenInfo info) => info.Value switch
    {
        TokenInfo.ValueType.SameAsPrevValue => SameAsPrevValueFlag,
        TokenInfo.ValueType.AsIsHiragana => AsIsHiraganaValueFlag,
        TokenInfo.ValueType.AsIsKatakana => AsIsKatakanaValueFlag,
        _ => NormalValueFlag,
    };

    private static byte GetFlagsForToken(IReadOnlyList<TokenInfo> tokens, int index)
    {
        byte flags = 0;
        if (index == tokens.Count - 1)
        {
            flags |= LastTokenFlag;
        }
        TokenInfo info = tokens[index];
        if ((info.Token.Attributes & Token.Attribute.SpellingCorrection) != 0)
        {
            flags |= SpellingCorrectionFlag;
        }
        flags |= GetFlagForPos(info);
        if (index == 0 && (flags & PosTypeFlagMask) == SameAsPrevPosFlag)
        {
            throw new InvalidOperationException("first token cannot become SameAsPrevPos");
        }
        flags |= GetFlagForValue(info);
        if (index == 0 && (flags & ValueTypeFlagMask) == SameAsPrevValueFlag)
        {
            throw new InvalidOperationException("first token cannot become SameAsPrevValue");
        }
        if ((flags & UpperCrammedIdMask) == 0)
        {
            // 下位 6bit が空いているので value trie id の格納に流用する。
            flags |= CrammedIdFlag;
        }
        return flags;
    }

    private static void EncodeCost(TokenInfo info, Span<byte> dst, ref int offset)
    {
        Token token = info.Token;
        if (token.Cost > CostMax)
        {
            throw new InvalidOperationException("cost must be within 15 bits");
        }
        if (info.Cost == TokenInfo.CostType.CanUseSmallEncoding)
        {
            dst[offset] = (byte)((token.Cost >> 8) | SmallCostFlag);
            offset += 1;
        }
        else
        {
            dst[offset] = (byte)(token.Cost >> 8);
            dst[offset + 1] = (byte)(token.Cost & 0xff);
            offset += 2;
        }
    }

    private static void EncodePos(TokenInfo info, byte flags, Span<byte> dst, ref int offset)
    {
        Token token = info.Token;
        int lid = token.Lid;
        int rid = token.Rid;
        switch (flags & PosTypeFlagMask)
        {
            case FullPosFlag:
                dst[offset] = (byte)(lid & 255);
                dst[offset + 1] = (byte)(((rid << 4) & 255) | (lid >> 8));
                dst[offset + 2] = (byte)((rid >> 4) & 255);
                offset += 3;
                break;
            case MonoPosFlag:
                dst[offset] = (byte)(lid & 255);
                dst[offset + 1] = (byte)(lid >> 8);
                offset += 2;
                break;
            case FrequentPosFlag:
                if (info.IdInFrequentPosMap < 0)
                {
                    throw new InvalidOperationException("id_in_frequent_pos_map must be set");
                }
                dst[offset] = (byte)info.IdInFrequentPosMap;
                offset += 1;
                break;
            case SameAsPrevPosFlag:
                break;
        }
    }

    private static void EncodeValueInfo(TokenInfo info, byte flags, Span<byte> dst, ref int offset)
    {
        if ((flags & ValueTypeFlagMask) != NormalValueFlag)
        {
            return; // word trie id を格納する必要がない。
        }
        int id = info.IdInValueTrie;
        if (id > ValueTrieIdMax)
        {
            throw new InvalidOperationException("too large word trie id (must be < 2^22)");
        }
        if ((flags & CrammedIdFlag) != 0)
        {
            dst[offset] = (byte)(id & 255);
            dst[offset + 1] = (byte)((id >> 8) & 255);
            // 上位 6bit を flags バイト(dst[0])の下位 6bit に詰める。
            dst[0] |= (byte)((id >> 16) & UpperCrammedIdMask);
            offset += 2;
        }
        else
        {
            dst[offset] = (byte)(id & 255);
            dst[offset + 1] = (byte)((id >> 8) & 255);
            dst[offset + 2] = (byte)((id >> 16) & 255);
            offset += 3;
        }
    }

    // codec.cc DecodeToken 相当。ptr の先頭からトークン 1 件を読み tokenInfo に格納。
    // 戻り値 true=次トークンあり / false=最終トークン。readBytes に消費バイト数。
    public bool DecodeToken(ReadOnlySpan<byte> ptr, TokenInfo tokenInfo, out int readBytes)
    {
        byte flags = ReadFlags(ptr[0]);
        if ((flags & SpellingCorrectionFlag) != 0)
        {
            tokenInfo.Token.Attributes = Token.Attribute.SpellingCorrection;
        }

        int offset = 1;
        DecodePos(ptr, flags, tokenInfo, ref offset);
        DecodeCost(ptr, tokenInfo, ref offset);
        DecodeValueInfo(ptr, flags, tokenInfo, ref offset);
        readBytes = offset;
        return (flags & LastTokenFlag) == 0;
    }

    // crammed のときは下位 6bit が value id の上位ビットなので、フラグは上位 2bit のみ有効。
    private static byte ReadFlags(byte val)
        => (val & CrammedIdFlag) != 0 ? (byte)(val & UpperFlagsMask) : val;

    private static void DecodePos(ReadOnlySpan<byte> ptr, byte flags, TokenInfo info, ref int offset)
    {
        Token token = info.Token;
        switch (flags & PosTypeFlagMask)
        {
            case FrequentPosFlag:
                info.Pos = TokenInfo.PosType.FrequentPos;
                info.IdInFrequentPosMap = ptr[offset];
                offset += 1;
                break;
            case SameAsPrevPosFlag:
                info.Pos = TokenInfo.PosType.SameAsPrevPos;
                break;
            case MonoPosFlag:
            {
                int id = (ptr[offset + 1] << 8) | ptr[offset];
                token.Lid = (ushort)id;
                token.Rid = (ushort)id;
                offset += 2;
                break;
            }
            case FullPosFlag:
                token.Lid = (ushort)(ptr[offset] + ((ptr[offset + 1] & 0x0f) << 8));
                token.Rid = (ushort)((ptr[offset + 1] >> 4) + (ptr[offset + 2] << 4));
                offset += 3;
                break;
        }
    }

    private static void DecodeCost(ReadOnlySpan<byte> ptr, TokenInfo info, ref int offset)
    {
        if ((ptr[offset] & SmallCostFlag) != 0)
        {
            info.Token.Cost = (ptr[offset] & SmallCostMask) << 8;
            offset += 1;
        }
        else
        {
            info.Token.Cost = (ptr[offset] << 8) + ptr[offset + 1];
            offset += 2;
        }
    }

    private static void DecodeValueInfo(ReadOnlySpan<byte> ptr, byte flags, TokenInfo info, ref int offset)
    {
        switch (flags & ValueTypeFlagMask)
        {
            case AsIsHiraganaValueFlag:
                info.Value = TokenInfo.ValueType.AsIsHiragana;
                break;
            case AsIsKatakanaValueFlag:
                info.Value = TokenInfo.ValueType.AsIsKatakana;
                break;
            case SameAsPrevValueFlag:
                info.Value = TokenInfo.ValueType.SameAsPrevValue;
                break;
            case NormalValueFlag:
            {
                info.Value = TokenInfo.ValueType.DefaultValue;
                int id = (ptr[offset + 1] << 8) | ptr[offset];
                if ((flags & CrammedIdFlag) != 0)
                {
                    id |= (ptr[0] & UpperCrammedIdMask) << 16;
                    offset += 2;
                }
                else
                {
                    id |= ptr[offset + 2] << 16;
                    offset += 3;
                }
                info.IdInValueTrie = id;
                break;
            }
        }
    }

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
