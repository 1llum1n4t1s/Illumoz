using Mozc.Os.Linux;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Os.Linux.Tests;

public class X11KeysymTests
{
    [Fact]
    public void PrintableAscii_MapsToKeyCode()
    {
        Pb.KeyEvent ke = X11Keysym.Translate(0x61, 0); // 'a'
        Assert.True(ke.HasKeyCode);
        Assert.Equal(0x61u, ke.KeyCode);
        Assert.False(ke.HasSpecialKey);
    }

    [Fact]
    public void SpecialKeys_Mapped()
    {
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Enter, X11Keysym.Translate(0xff0d, 0).SpecialKey);
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Backspace, X11Keysym.Translate(0xff08, 0).SpecialKey);
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Space, X11Keysym.Translate(0x20, 0).SpecialKey);
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Left, X11Keysym.Translate(0xff51, 0).SpecialKey);
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.F6, X11Keysym.Translate(0xffc3, 0).SpecialKey);
    }

    [Fact]
    public void KeypadKeysyms_MapToSpecialKeys()
    {
        // テンキー(KP_*)は対応する特殊キー/数字へマップする(C++ ibus keysym 変換に合わせる)。
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Enter, X11Keysym.Translate(0xff8d, 0).SpecialKey); // KP_Enter
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Home, X11Keysym.Translate(0xff95, 0).SpecialKey);  // KP_Home
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Numpad0, X11Keysym.Translate(0xffb0, 0).SpecialKey); // KP_0
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Numpad9, X11Keysym.Translate(0xffb9, 0).SpecialKey); // KP_9
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Divide, X11Keysym.Translate(0xffaf, 0).SpecialKey); // KP_Divide
    }

    [Fact]
    public void Modifiers_FromStateMask()
    {
        Pb.KeyEvent ke = X11Keysym.Translate(0x61, (1u << 2) | (1u << 0)); // Control+Shift
        Assert.Contains(Pb.KeyEvent.Types.ModifierKey.Ctrl, ke.ModifierKeys);
        Assert.Contains(Pb.KeyEvent.Types.ModifierKey.Shift, ke.ModifierKeys);
    }

    [Fact]
    public void UnicodeKeysym_StripsHighBits()
    {
        // 0x01000000 | 0x3042(あ)
        Pb.KeyEvent ke = X11Keysym.Translate(0x01003042, 0);
        Assert.Equal(0x3042u, ke.KeyCode);
    }
}
