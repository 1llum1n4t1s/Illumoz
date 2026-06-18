using Mozc.Os.Mac;
using Xunit;
using Pb = Mozc.Commands;

namespace Mozc.Os.Mac.Tests;

public class MacKeyTranslatorTests
{
    [Fact]
    public void Printable_FromCharacters()
    {
        Pb.KeyEvent ke = MacKeyTranslator.Translate(0, "a", 0);
        Assert.Equal((uint)'a', ke.KeyCode);
    }

    [Fact]
    public void VirtualKeys_Mapped()
    {
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Enter, MacKeyTranslator.Translate(36, "", 0).SpecialKey);
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Backspace, MacKeyTranslator.Translate(51, "", 0).SpecialKey);
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Space, MacKeyTranslator.Translate(49, " ", 0).SpecialKey);
        Assert.Equal(Pb.KeyEvent.Types.SpecialKey.Left, MacKeyTranslator.Translate(123, "", 0).SpecialKey);
    }

    [Fact]
    public void Modifiers_FromFlags()
    {
        Pb.KeyEvent ke = MacKeyTranslator.Translate(0, "a", (1u << 18) | (1u << 19)); // Control+Option
        Assert.Contains(Pb.KeyEvent.Types.ModifierKey.Ctrl, ke.ModifierKeys);
        Assert.Contains(Pb.KeyEvent.Types.ModifierKey.Alt, ke.ModifierKeys);
    }
}
