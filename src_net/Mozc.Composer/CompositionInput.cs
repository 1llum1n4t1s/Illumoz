namespace Mozc.Composer;

// C++ composer::CompositionInput 相当(必要最小)。1 回の入力イベント。
public sealed class CompositionInput
{
    public string Raw { get; set; } = string.Empty;
    // かな入力等で「変換済み文字」を直接与える場合に使用(ローマ字では空)。
    public string Conversion { get; set; } = string.Empty;
    public bool IsNewInput { get; set; }
    public bool IsAsis { get; set; }

    public bool Empty => Raw.Length == 0 && Conversion.Length == 0;

    public void Clear()
    {
        Raw = string.Empty;
        Conversion = string.Empty;
        IsAsis = false;
    }

    public static CompositionInput FromRaw(string raw, bool isNewInput = false)
        => new() { Raw = raw, IsNewInput = isNewInput };
}
