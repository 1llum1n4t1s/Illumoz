namespace Mozc.Storage.Louds;

// C++ src/storage/louds/bit_stream.{h,cc} 相当。
// bit を LSB-first で byte 列に詰める(*last |= (bit&1) << (num_bits%8))。
public sealed class BitStream
{
    private readonly List<byte> _image = new();
    private int _numBits;

    public int ByteSize => _image.Count;
    public int NumBits => _numBits;

    public void PushBit(int bit)
    {
        int shift = _numBits % 8;
        if (shift == 0)
        {
            _image.Add(0);
        }
        _image[^1] |= (byte)((bit & 1) << shift);
        _numBits++;
    }

    // image の byte 長を 4 の倍数に揃える(= 32bit 境界)。
    public void FillPadding32()
    {
        int remaining = _image.Count % 4;
        if (remaining != 0)
        {
            for (int i = 0; i < 4 - remaining; i++)
            {
                _image.Add(0);
            }
        }
        _numBits = _image.Count * 8;
    }

    public void CopyTo(List<byte> dest) => dest.AddRange(_image);

    // C++ internal::PushInt32。little-endian 4 バイトを追記。
    public static void PushInt32(List<byte> image, uint value)
    {
        image.Add((byte)(value & 0xFF));
        image.Add((byte)((value >> 8) & 0xFF));
        image.Add((byte)((value >> 16) & 0xFF));
        image.Add((byte)((value >> 24) & 0xFF));
    }
}
