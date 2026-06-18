using System.Text;

namespace Mozc.Base;

// C++ src/base/hash.cc の LegacyFingerprint 系(Jenkins lookup2)相当。
// 辞書ファイルの節名→fingerprint 等に使われるためバイト互換が必須。
// hash_test.cc の既知ベクタで検証可能。
public static class LegacyFingerprint
{
    private const uint Seed32 = 0xfd12deff; // kFingerPrint32Seed
    private const uint Seed0 = 0x6d6f;       // kFingerPrintSeed0 (64bit hi 既定)
    private const uint Seed1 = 0x7a63;       // kFingerPrintSeed1 (64bit lo)

    public static uint Fingerprint32(ReadOnlySpan<byte> str) => Fingerprint32WithSeed(str, Seed32);
    public static uint Fingerprint32(string str) => Fingerprint32(Encoding.UTF8.GetBytes(str));

    public static ulong Fingerprint(ReadOnlySpan<byte> str) => FingerprintWithSeed(str, Seed0);
    public static ulong Fingerprint(string str) => Fingerprint(Encoding.UTF8.GetBytes(str));

    public static ulong FingerprintWithSeed(ReadOnlySpan<byte> str, uint seed)
    {
        uint hi = Fingerprint32WithSeed(str, seed);
        uint lo = Fingerprint32WithSeed(str, Seed1);
        ulong result = ((ulong)hi << 32) | lo;
        if (hi == 0 && lo < 2)
        {
            result ^= 0x130f9bef94a0a928UL;
        }
        return result;
    }

    public static ulong FingerprintWithSeed(string str, uint seed)
        => FingerprintWithSeed(Encoding.UTF8.GetBytes(str), seed);

    public static uint Fingerprint32WithSeed(ReadOnlySpan<byte> str, uint seed)
    {
        uint strLen = (uint)str.Length;
        uint a = 0x9e3779b9, b = 0x9e3779b9, c = seed;
        int p = 0;
        int n = str.Length;

        while (n >= 12)
        {
            a += ToUint32(str[p], str[p + 1], str[p + 2], str[p + 3]);
            b += ToUint32(str[p + 4], str[p + 5], str[p + 6], str[p + 7]);
            c += ToUint32(str[p + 8], str[p + 9], str[p + 10], str[p + 11]);
            Mix(ref a, ref b, ref c);
            p += 12;
            n -= 12;
        }

        c += strLen;
        switch (n)
        {
            case 11: c += (uint)str[p + 10] << 24; goto case 10;
            case 10: c += (uint)str[p + 9] << 16; goto case 9;
            case 9: c += (uint)str[p + 8] << 8; goto case 8;
            case 8: b += (uint)str[p + 7] << 24; goto case 7;
            case 7: b += (uint)str[p + 6] << 16; goto case 6;
            case 6: b += (uint)str[p + 5] << 8; goto case 5;
            case 5: b += str[p + 4]; goto case 4;
            case 4: a += (uint)str[p + 3] << 24; goto case 3;
            case 3: a += (uint)str[p + 2] << 16; goto case 2;
            case 2: a += (uint)str[p + 1] << 8; goto case 1;
            case 1: a += str[p]; break;
            case 0: break;
        }
        Mix(ref a, ref b, ref c);
        return c;
    }

    private static uint ToUint32(byte a, byte b, byte c, byte d)
        => a + ((uint)b << 8) + ((uint)c << 16) + ((uint)d << 24);

    // 古典的 Jenkins lookup2 mix(シフト 13/8/13/12/16/5/3/10/15)。uint は既定で wrap。
    private static void Mix(ref uint a, ref uint b, ref uint c)
    {
        a -= b; a -= c; a ^= c >> 13;
        b -= c; b -= a; b ^= a << 8;
        c -= a; c -= b; c ^= b >> 13;
        a -= b; a -= c; a ^= c >> 12;
        b -= c; b -= a; b ^= a << 16;
        c -= a; c -= b; c ^= b >> 5;
        a -= b; a -= c; a ^= c >> 3;
        b -= c; b -= a; b ^= a << 10;
        c -= a; c -= b; c ^= b >> 15;
    }
}
