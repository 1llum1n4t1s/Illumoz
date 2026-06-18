using Mozc.Gui;
using Xunit;

namespace Mozc.Gui.Tests;

public class CandidateWindowViewModelTests
{
    [Fact]
    public void Update_PopulatesItemsAndFocus()
    {
        var vm = new CandidateWindowViewModel();
        vm.Update(new[] { ("私", "名詞"), ("渡し", "名詞"), ("わたし", "") }, 1);

        Assert.True(vm.IsVisible);
        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(1, vm.FocusedIndex);
        Assert.False(vm.Items[0].IsFocused);
        Assert.True(vm.Items[1].IsFocused);
        // ショートカット番号は 1-based(10件目は 0)。
        Assert.Equal("1", vm.Items[0].Shortcut);
        Assert.Equal("2", vm.Items[1].Shortcut);
    }

    [Fact]
    public void Update_Empty_Hides()
    {
        var vm = new CandidateWindowViewModel();
        vm.Update(global::System.Array.Empty<(string, string)>(), -1);
        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Hide_Clears()
    {
        var vm = new CandidateWindowViewModel();
        vm.Update(new[] { ("私", "") }, 0);
        vm.Hide();
        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Items);
        Assert.Equal(-1, vm.FocusedIndex);
    }
}
