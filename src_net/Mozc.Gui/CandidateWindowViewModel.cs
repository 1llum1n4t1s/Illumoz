using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mozc.Gui;

// 候補ウィンドウ 1 行(ショートカット番号 + 候補値 + 注釈 + ハイライト)。
public sealed partial class CandidateItemViewModel : ObservableObject
{
    public CandidateItemViewModel(int index, string value, string description)
        : this(index, value, description, ((index + 1) % 10).ToString())
    {
    }

    public CandidateItemViewModel(int index, string value, string description, string shortcut)
    {
        Index = index;
        // サーバ提供のショートカットがあれば優先、無ければ番号(1..9,0)。
        Shortcut = shortcut.Length > 0 ? shortcut : ((index + 1) % 10).ToString();
        Value = value;
        Description = description;
    }

    public int Index { get; }
    public string Shortcut { get; }
    public string Value { get; }
    public string Description { get; }

    [ObservableProperty] private bool _isFocused;
}

// C++ src/renderer の候補ウィンドウ表示状態(View 非依存)。候補一覧とフォーカス、
// 表示/非表示を保持。RendererCommand(Output.CandidateWindow)から更新する。
public sealed partial class CandidateWindowViewModel : ObservableObject
{
    public ObservableCollection<CandidateItemViewModel> Items { get; } = new();

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private int _focusedIndex = -1;

    // 1 ページの候補数(C++ 既定は 9)。
    public int PageSize { get; set; } = 9;

    // フォーカス候補が属するページ番号(0 始まり)。
    public int FocusedPage => FocusedIndex < 0 ? 0 : FocusedIndex / PageSize;

    public int PageCount => Items.Count == 0 ? 0 : (Items.Count + PageSize - 1) / PageSize;

    // フォーカス候補のページに属する候補のみ返す(候補窓の表示分割)。
    public IReadOnlyList<CandidateItemViewModel> PageItems()
    {
        int start = FocusedPage * PageSize;
        var page = new List<CandidateItemViewModel>();
        for (int i = start; i < Items.Count && i < start + PageSize; i++)
        {
            page.Add(Items[i]);
        }
        return page;
    }

    // 候補(値/注釈)とフォーカス位置で更新。空なら非表示。
    public void Update(IReadOnlyList<(string Value, string Description)> candidates, int focusedIndex)
    {
        Items.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            Items.Add(new CandidateItemViewModel(i, candidates[i].Value, candidates[i].Description)
            {
                IsFocused = i == focusedIndex,
            });
        }
        FocusedIndex = focusedIndex;
        IsVisible = candidates.Count > 0;
    }

    // サーバ提供のショートカット付きで更新する(SelectionShortcut を尊重)。
    public void Update(IReadOnlyList<(string Value, string Description, string Shortcut)> candidates, int focusedIndex)
    {
        Items.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            Items.Add(new CandidateItemViewModel(i, candidates[i].Value, candidates[i].Description, candidates[i].Shortcut)
            {
                IsFocused = i == focusedIndex,
            });
        }
        FocusedIndex = focusedIndex;
        IsVisible = candidates.Count > 0;
    }

    public void Hide()
    {
        Items.Clear();
        FocusedIndex = -1;
        IsVisible = false;
    }
}
