using Mozc.Gui;
using Xunit;

namespace Mozc.Gui.Tests;

public class DictionaryToolViewModelTests
{
    private static UserDictionaryEntry E(string k, string v) => new(k, v, "名詞", "");

    [Fact]
    public void AddAndDelete()
    {
        var vm = new DictionaryToolViewModel();
        Assert.False(vm.DeleteSelectedCommand.CanExecute(null));

        vm.Add(E("もずく", "もずく酢"));
        vm.Add(E("わたし", "私"));
        Assert.Equal(2, vm.Count);

        vm.SelectedEntry = vm.Entries[0];
        Assert.True(vm.DeleteSelectedCommand.CanExecute(null));
        vm.DeleteSelectedCommand.Execute(null);
        Assert.Equal(1, vm.Count);
        Assert.Equal("私", vm.Entries[0].Value);
    }

    [Fact]
    public void Find_PartialMatch()
    {
        var vm = new DictionaryToolViewModel();
        vm.Add(E("とうきょう", "東京"));
        vm.Add(E("とうほく", "東北"));
        vm.Add(E("おおさか", "大阪"));
        var hits = new List<UserDictionaryEntry>(vm.Find("とう"));
        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void ImportTsv_AddsRows()
    {
        var vm = new DictionaryToolViewModel();
        int n = vm.ImportTsv("もずく\tもずく酢\t名詞\t食品\nあ\tぁ\t名詞\nbad_line");
        Assert.Equal(2, n);
        Assert.Equal("食品", vm.Entries[0].Comment);
    }
}

public class ConfigViewModelTests
{
    [Fact]
    public void Apply_EmitsConfig()
    {
        var vm = new ConfigViewModel { InputMode = InputModeSetting.Kana, SuggestionsSize = 5 };
        MozcConfig? cfg = null;
        vm.Applied += c => cfg = c;
        Assert.True(vm.ApplyCommand.CanExecute(null));
        vm.ApplyCommand.Execute(null);
        Assert.NotNull(cfg);
        Assert.Equal(InputModeSetting.Kana, cfg!.InputMode);
        Assert.Equal(5, cfg.SuggestionsSize);
    }

    [Fact]
    public void Apply_DisabledWhenSuggestionsOutOfRange()
    {
        var vm = new ConfigViewModel { SuggestionsSize = 0 };
        Assert.False(vm.ApplyCommand.CanExecute(null));
        vm.SuggestionsSize = 10;
        Assert.False(vm.ApplyCommand.CanExecute(null));
        vm.SuggestionsSize = 9;
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var vm = new ConfigViewModel
        {
            InputMode = InputModeSetting.Kana, SuggestionsSize = 7, UseHistorySuggest = false,
        };
        vm.ResetCommand.Execute(null);
        Assert.Equal(InputModeSetting.Romaji, vm.InputMode);
        Assert.Equal(3, vm.SuggestionsSize);
        Assert.True(vm.UseHistorySuggest);
    }
}
