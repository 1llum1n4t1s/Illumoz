using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mozc.Gui;

public enum InputModeSetting { Romaji, Kana }
public enum PunctuationSetting { KutenTouten, CommaPeriod, CommaTouten, KutenPeriod }
public enum SpaceCharSetting { Follow, Fullwidth, Halfwidth }

// 設定スナップショット(C++ config::Config の主要項目)。Session/Engine へ渡す。
public sealed record MozcConfig(
    InputModeSetting InputMode,
    PunctuationSetting Punctuation,
    SpaceCharSetting SpaceChar,
    bool UseHistorySuggest,
    int SuggestionsSize);

// C++ src/gui/config_dialog 相当の ViewModel(設定ダイアログ)。
// 各設定を編集し、Apply で MozcConfig を発火、Reset で既定へ戻す。
public sealed partial class ConfigViewModel : ObservableObject
{
    public static IReadOnlyList<InputModeSetting> InputModes { get; } =
        new[] { InputModeSetting.Romaji, InputModeSetting.Kana };

    public static IReadOnlyList<PunctuationSetting> Punctuations { get; } = new[]
    {
        PunctuationSetting.KutenTouten, PunctuationSetting.CommaPeriod,
        PunctuationSetting.CommaTouten, PunctuationSetting.KutenPeriod,
    };

    // XAML compiled binding 用インスタンス公開。
    public IReadOnlyList<InputModeSetting> InputModeList => InputModes;
    public IReadOnlyList<PunctuationSetting> PunctuationList => Punctuations;

    [ObservableProperty] private InputModeSetting _inputMode = InputModeSetting.Romaji;
    [ObservableProperty] private PunctuationSetting _punctuation = PunctuationSetting.KutenTouten;
    [ObservableProperty] private SpaceCharSetting _spaceChar = SpaceCharSetting.Follow;
    [ObservableProperty] private bool _useHistorySuggest = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    private int _suggestionsSize = 3;

    public event global::System.Action<MozcConfig>? Applied;

    // C++ の制約: suggestions は 1..9。
    public bool CanApply => SuggestionsSize is >= 1 and <= 9;

    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        Applied?.Invoke(new MozcConfig(
            InputMode, Punctuation, SpaceChar, UseHistorySuggest, SuggestionsSize));
    }

    [RelayCommand]
    private void Reset()
    {
        InputMode = InputModeSetting.Romaji;
        Punctuation = PunctuationSetting.KutenTouten;
        SpaceChar = SpaceCharSetting.Follow;
        UseHistorySuggest = true;
        SuggestionsSize = 3;
    }
}
