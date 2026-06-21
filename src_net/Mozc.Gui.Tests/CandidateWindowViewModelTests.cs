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
    public void Update_HonorsServerShortcuts()
    {
        var vm = new CandidateWindowViewModel();
        vm.Update(new[] { ("私", "名詞", "a"), ("渡し", "名詞", "s"), ("わたし", "", "") }, 0);
        Assert.Equal("a", vm.Items[0].Shortcut);  // サーバ提供
        Assert.Equal("s", vm.Items[1].Shortcut);
        Assert.Equal("3", vm.Items[2].Shortcut);  // 空はフォールバック番号
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
    public void Paging_FocusedPageAndItems()
    {
        var vm = new CandidateWindowViewModel { PageSize = 3 };
        var cands = new List<(string, string)>();
        for (int i = 0; i < 8; i++)
        {
            cands.Add(($"c{i}", ""));
        }
        vm.Update(cands, focusedIndex: 4); // 4 は 2 ページ目(index 3-5)

        Assert.Equal(3, vm.PageCount);   // 8件/3 = 3ページ
        Assert.Equal(1, vm.FocusedPage); // 0始まり
        var page = vm.PageItems();
        Assert.Equal(3, page.Count);
        Assert.Equal("c3", page[0].Value);
        Assert.Equal("c5", page[2].Value);
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
