using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mozc.Gui;

// 候補ウィンドウ 1 行(ショートカット番号 + 候補値 + 注釈 + ハイライト)。
public sealed partial class CandidateItemViewModel : ObservableObject
{
    public CandidateItemViewModel(int index, string value, string description)
    {
        Index = index;
        Shortcut = ((index + 1) % 10).ToString();
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

    public void Hide()
    {
        Items.Clear();
        FocusedIndex = -1;
        IsVisible = false;
    }
}
