using Pb = Mozc.Commands;

namespace Mozc.Os.Linux;

// ibus(X11)の keyval(keysym) + state(modifier mask) を Mozc.Commands.KeyEvent に変換する。
// C++ src/unix/ibus/key_translator.cc 相当(主要キー)。GObject signal stub から渡る値を変換。
public static class X11Keysym
{
    // X11 modifier mask(ibus state)。
    private const uint ShiftMask = 1 << 0;
    private const uint ControlMask = 1 << 2;
    private const uint Mod1Mask = 1 << 3; // Alt

    // 代表的な特殊キー keysym。
    private static readonly Dictionary<uint, Pb.KeyEvent.Types.SpecialKey> Special = new()
    {
        [0xff08] = Pb.KeyEvent.Types.SpecialKey.Backspace,
        [0xff09] = Pb.KeyEvent.Types.SpecialKey.Tab,
        [0xff0d] = Pb.KeyEvent.Types.SpecialKey.Enter,
        [0xff1b] = Pb.KeyEvent.Types.SpecialKey.Escape,
        [0xff50] = Pb.KeyEvent.Types.SpecialKey.Home,
        [0xff51] = Pb.KeyEvent.Types.SpecialKey.Left,
        [0xff52] = Pb.KeyEvent.Types.SpecialKey.Up,
        [0xff53] = Pb.KeyEvent.Types.SpecialKey.Right,
        [0xff54] = Pb.KeyEvent.Types.SpecialKey.Down,
        [0xff55] = Pb.KeyEvent.Types.SpecialKey.PageUp,
        [0xff56] = Pb.KeyEvent.Types.SpecialKey.PageDown,
        [0xff57] = Pb.KeyEvent.Types.SpecialKey.End,
        [0xff63] = Pb.KeyEvent.Types.SpecialKey.Insert,
        [0xffff] = Pb.KeyEvent.Types.SpecialKey.Del,
        [0x020] = Pb.KeyEvent.Types.SpecialKey.Space,
        [0xff21] = Pb.KeyEvent.Types.SpecialKey.Kanji,    // Kanji
        [0xff2d] = Pb.KeyEvent.Types.SpecialKey.Kana,     // Kana_Lock approx
        [0xff7e] = Pb.KeyEvent.Types.SpecialKey.Hankaku,  // Mode_switch approx
    };

    public static Pb.KeyEvent Translate(uint keyval, uint state)
    {
        var ke = new Pb.KeyEvent();
        if ((state & ControlMask) != 0)
        {
            ke.ModifierKeys.Add(Pb.KeyEvent.Types.ModifierKey.Ctrl);
        }
        if ((state & Mod1Mask) != 0)
        {
            ke.ModifierKeys.Add(Pb.KeyEvent.Types.ModifierKey.Alt);
        }
        if ((state & ShiftMask) != 0)
        {
            ke.ModifierKeys.Add(Pb.KeyEvent.Types.ModifierKey.Shift);
        }

        if (Special.TryGetValue(keyval, out Pb.KeyEvent.Types.SpecialKey sk))
        {
            ke.SpecialKey = sk;
            return ke;
        }

        // F1-F12: keysym 0xffbe..0xffc9(proto enum 値の連番を仮定せず明示マップ)。
        Pb.KeyEvent.Types.SpecialKey? fkey = keyval switch
        {
            0xffbe => Pb.KeyEvent.Types.SpecialKey.F1,
            0xffbf => Pb.KeyEvent.Types.SpecialKey.F2,
            0xffc0 => Pb.KeyEvent.Types.SpecialKey.F3,
            0xffc1 => Pb.KeyEvent.Types.SpecialKey.F4,
            0xffc2 => Pb.KeyEvent.Types.SpecialKey.F5,
            0xffc3 => Pb.KeyEvent.Types.SpecialKey.F6,
            0xffc4 => Pb.KeyEvent.Types.SpecialKey.F7,
            0xffc5 => Pb.KeyEvent.Types.SpecialKey.F8,
            0xffc6 => Pb.KeyEvent.Types.SpecialKey.F9,
            0xffc7 => Pb.KeyEvent.Types.SpecialKey.F10,
            0xffc8 => Pb.KeyEvent.Types.SpecialKey.F11,
            0xffc9 => Pb.KeyEvent.Types.SpecialKey.F12,
            _ => null,
        };
        if (fkey != null)
        {
            ke.SpecialKey = fkey.Value;
            return ke;
        }

        // Latin-1 印字可能(keysym == Unicode codepoint)。
        if (keyval is >= 0x20 and <= 0x7e)
        {
            ke.KeyCode = keyval;
            return ke;
        }
        // Unicode keysym(0x01000000 | codepoint)。
        if ((keyval & 0xff000000) == 0x01000000)
        {
            ke.KeyCode = keyval & 0x00ffffff;
            return ke;
        }

        ke.SpecialKey = Pb.KeyEvent.Types.SpecialKey.UndefinedKey;
        return ke;
    }
}
