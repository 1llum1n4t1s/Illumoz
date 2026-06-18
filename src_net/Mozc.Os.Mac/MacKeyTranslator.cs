using Pb = Mozc.Commands;

namespace Mozc.Os.Mac;

// macOS IMK の NSEvent(keyCode 仮想キー + characters + modifierFlags)を
// Mozc.Commands.KeyEvent に変換する。C++ src/mac の KeyCodeMap 相当。
// ObjC IMKInputController サブクラス(極薄 native)が handleEvent からこの変換を呼ぶ。
public static class MacKeyTranslator
{
    // NSEventModifierFlags(上位ビット)。
    private const uint Shift = 1u << 17;
    private const uint Control = 1u << 18;
    private const uint Option = 1u << 19; // Alt

    // 代表的な仮想キーコード(HIToolbox Events.h)。
    private static readonly Dictionary<ushort, Pb.KeyEvent.Types.SpecialKey> Special = new()
    {
        [36] = Pb.KeyEvent.Types.SpecialKey.Enter,     // kVK_Return
        [76] = Pb.KeyEvent.Types.SpecialKey.Enter,     // kVK_ANSI_KeypadEnter
        [48] = Pb.KeyEvent.Types.SpecialKey.Tab,       // kVK_Tab
        [49] = Pb.KeyEvent.Types.SpecialKey.Space,     // kVK_Space
        [51] = Pb.KeyEvent.Types.SpecialKey.Backspace, // kVK_Delete
        [117] = Pb.KeyEvent.Types.SpecialKey.Del,      // kVK_ForwardDelete
        [53] = Pb.KeyEvent.Types.SpecialKey.Escape,    // kVK_Escape
        [123] = Pb.KeyEvent.Types.SpecialKey.Left,
        [124] = Pb.KeyEvent.Types.SpecialKey.Right,
        [125] = Pb.KeyEvent.Types.SpecialKey.Down,
        [126] = Pb.KeyEvent.Types.SpecialKey.Up,
        [115] = Pb.KeyEvent.Types.SpecialKey.Home,
        [119] = Pb.KeyEvent.Types.SpecialKey.End,
        [116] = Pb.KeyEvent.Types.SpecialKey.PageUp,
        [121] = Pb.KeyEvent.Types.SpecialKey.PageDown,
    };

    public static Pb.KeyEvent Translate(ushort keyCode, string characters, uint modifierFlags)
    {
        var ke = new Pb.KeyEvent();
        if ((modifierFlags & Control) != 0)
        {
            ke.ModifierKeys.Add(Pb.KeyEvent.Types.ModifierKey.Ctrl);
        }
        if ((modifierFlags & Option) != 0)
        {
            ke.ModifierKeys.Add(Pb.KeyEvent.Types.ModifierKey.Alt);
        }
        if ((modifierFlags & Shift) != 0)
        {
            ke.ModifierKeys.Add(Pb.KeyEvent.Types.ModifierKey.Shift);
        }

        if (Special.TryGetValue(keyCode, out Pb.KeyEvent.Types.SpecialKey sk))
        {
            ke.SpecialKey = sk;
            return ke;
        }

        // 印字文字(characters の先頭コードポイント)。
        if (!string.IsNullOrEmpty(characters))
        {
            System.Text.Rune.DecodeFromUtf16(characters, out System.Text.Rune rune, out _);
            ke.KeyCode = (uint)rune.Value;
            return ke;
        }

        ke.SpecialKey = Pb.KeyEvent.Types.SpecialKey.UndefinedKey;
        return ke;
    }
}
