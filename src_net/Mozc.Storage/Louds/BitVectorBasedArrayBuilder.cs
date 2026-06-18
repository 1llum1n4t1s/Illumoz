namespace Mozc.Storage.Louds;

// C++ src/storage/louds/bit_vector_based_array_builder.{h,cc} 相当。
public sealed class BitVectorBasedArrayBuilder
{
    private readonly List<byte[]> _elements = new();
    private int _baseLength;
    private int _stepLength;
    private bool _built;

    public void Add(byte[] element)
    {
        if (_built) throw new InvalidOperationException("already built");
        _elements.Add(element);
    }

    public void SetSize(int baseLength, int stepLength)
    {
        if (_built) throw new InvalidOperationException("already built");
        _baseLength = baseLength;
        _stepLength = stepLength;
    }

    public byte[] Build()
    {
        if (_built) throw new InvalidOperationException("already built");

        var bitStream = new BitStream();
        var data = new List<byte>();

        foreach (byte[] element in _elements)
        {
            int numSteps = 0;
            for (int length = element.Length; length > _baseLength; length -= _stepLength)
            {
                numSteps++;
            }
            int outputLength = _baseLength + numSteps * _stepLength;

            bitStream.PushBit(0);
            for (int j = 0; j < numSteps; j++)
            {
                bitStream.PushBit(1);
            }
            data.AddRange(element);
            for (int p = 0; p < outputLength - element.Length; p++)
            {
                data.Add(0); // '\0' パディング
            }
        }
        bitStream.PushBit(0); // 番兵
        bitStream.FillPadding32();

        var image = new List<byte>();
        BitStream.PushInt32(image, (uint)bitStream.ByteSize);
        BitStream.PushInt32(image, (uint)_baseLength);
        BitStream.PushInt32(image, (uint)_stepLength);
        BitStream.PushInt32(image, 0);
        bitStream.CopyTo(image);
        image.AddRange(data);

        _built = true;
        return image.ToArray();
    }
}
