using System.Buffers.Binary;
using Mozc.Base;
using Mozc.Dictionary;
using Mozc.Dictionary.System;
using Mozc.Storage;

namespace Mozc.Converter;

// C5: mozc.data(DataSet)を開き、各セクションを対応 reader に供給する層。
// C++ data_manager.cc 相当(変換に必要な中核セクションのみ)。
// セクション名は data_manager.cc の reader.Get(...) に一致。
public sealed class DataManager
{
    private readonly DataSetReader _reader = new();

    public DataManager(byte[] mozcData)
    {
        if (!_reader.Init(mozcData, MozcConstants.DataSetMagicOss))
        {
            throw new InvalidDataException("invalid mozc.data (DataSet magic mismatch)");
        }
    }

    public SystemDictionary GetSystemDictionary()
        => SystemDictionary.OpenFromDictionaryImage(_reader.Get("dict").ToArray());

    public Connector GetConnector()
        => Connector.Create(_reader.Get("conn").ToArray());

    public PosMatcher GetPosMatcher()
        => PosMatcher.FromBytes(_reader.Get("pos_matcher").Span, PosMatcher.RuleCount);

    public Segmenter GetSegmenter()
    {
        (int lsize, int rsize) = ParseSegmenterSizeInfo(_reader.Get("segmenter_sizeinfo").Span);
        ushort[] lTable = ToUshortArray(_reader.Get("segmenter_ltable").Span);
        ushort[] rTable = ToUshortArray(_reader.Get("segmenter_rtable").Span);
        byte[] bitarray = _reader.Get("segmenter_bitarray").ToArray();
        ushort[] boundary = ToUshortArray(_reader.Get("bdry").Span);
        return new Segmenter(lsize, rsize, lTable, rTable, bitarray, boundary);
    }

    // proto2 SegmenterDataSizeInfo: field1=compressed_lsize, field2=compressed_rsize(uint64 varint)。
    public static (int Lsize, int Rsize) ParseSegmenterSizeInfo(ReadOnlySpan<byte> data)
    {
        int lsize = 0;
        int rsize = 0;
        int p = 0;
        while (p < data.Length)
        {
            int tag = data[p++];
            ulong value = ReadVarint(data, ref p);
            if (tag == 0x08)
            {
                lsize = (int)value;
            }
            else if (tag == 0x10)
            {
                rsize = (int)value;
            }
        }
        return (lsize, rsize);
    }

    // SegmenterDataSizeInfo を直列化(DataSetBuilder 用)。
    public static byte[] SerializeSegmenterSizeInfo(int lsize, int rsize)
    {
        var buf = new List<byte> { 0x08 };
        WriteVarint(buf, (ulong)lsize);
        buf.Add(0x10);
        WriteVarint(buf, (ulong)rsize);
        return buf.ToArray();
    }

    private static ushort[] ToUshortArray(ReadOnlySpan<byte> bytes)
    {
        var arr = new ushort[bytes.Length / 2];
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i * 2));
        }
        return arr;
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int p)
    {
        ulong result = 0;
        int shift = 0;
        while (p < data.Length)
        {
            byte b = data[p++];
            result |= (ulong)(b & 0x7f) << shift;
            if ((b & 0x80) == 0)
            {
                break;
            }
            shift += 7;
        }
        return result;
    }

    private static void WriteVarint(List<byte> buf, ulong value)
    {
        while (value >= 0x80)
        {
            buf.Add((byte)(value | 0x80));
            value >>= 7;
        }
        buf.Add((byte)value);
    }
}
