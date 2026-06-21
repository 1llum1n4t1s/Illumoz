using Mozc.Gui;
using Xunit;

namespace Mozc.Gui.Tests;

public class WordRegisterViewModelTests
{
    [Fact]
    public void CannotRegister_WhenReadingOrWordEmpty()
    {
        var vm = new WordRegisterViewModel();
        Assert.False(vm.CanRegister);
        Assert.False(vm.RegisterCommand.CanExecute(null));

        vm.Reading = "もずく";
        Assert.False(vm.CanRegister); // word 未入力

        vm.Word = "もずく酢";
        Assert.True(vm.CanRegister);
        Assert.True(vm.RegisterCommand.CanExecute(null));
    }

    [Fact]
    public void Register_RaisesEventWithTrimmedEntry()
    {
        var vm = new WordRegisterViewModel
        {
            Reading = "  もずく  ",
            Word = " もずく酢 ",
            Pos = "名詞",
            Comment = " 食品 ",
        };
        UserDictionaryEntry? captured = null;
        vm.Registered += e => captured = e;

        vm.RegisterCommand.Execute(null);

        Assert.NotNull(captured);
        Assert.Equal("もずく", captured!.Key);
        Assert.Equal("もずく酢", captured.Value);
        Assert.Equal("名詞", captured.Pos);
        Assert.Equal("食品", captured.Comment);
    }

    [Fact]
    public void CanExecuteChanged_FiresWhenInputsChange()
    {
        var vm = new WordRegisterViewModel();
        bool fired = false;
        vm.RegisterCommand.CanExecuteChanged += (_, _) => fired = true;
        vm.Reading = "あ";
        Assert.True(fired); // [NotifyCanExecuteChangedFor] により再評価通知
    }

    [Fact]
    public void PosCandidates_ContainsCommonCategories()
    {
        Assert.Contains("名詞", WordRegisterViewModel.PosCandidates);
        Assert.Contains("人名", WordRegisterViewModel.PosCandidates);
        Assert.Contains("抑制単語", WordRegisterViewModel.PosCandidates);
    }
}
