using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mozc.Gui;

// ユーザー辞書に登録する 1 エントリ(C++ user_dictionary_storage の UserDictionaryEntry 相当)。
public sealed record UserDictionaryEntry(string Key, string Value, string Pos, string Comment);

// C++ src/gui/word_register_dialog 相当の ViewModel(単語登録ダイアログ)。
// 読み・単語・品詞・コメントを編集し、妥当なら登録イベントを発火する。
// View(XAML)は後続。ロジックはここで完結しヘッドレス検証可能。
public sealed partial class WordRegisterViewModel : ObservableObject
{
    // 代表的な品詞(C++ user_pos の主要分類)。
    public static IReadOnlyList<string> PosCandidates { get; } = new[]
    {
        "名詞", "短縮よみ", "サ変名詞", "固有名詞", "人名", "姓", "名",
        "組織", "地名", "名詞サ変", "形容詞", "副詞", "動詞一段", "動詞五段",
        "接頭語", "接尾一般", "顔文字", "記号", "抑制単語",
    };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _reading = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
    private string _word = string.Empty;

    [ObservableProperty]
    private string _pos = "名詞";

    [ObservableProperty]
    private string _comment = string.Empty;

    // 登録確定時に発火(View/呼び出し側が UserDictionaryStore へ保存)。
    public event global::System.Action<UserDictionaryEntry>? Registered;

    public bool CanRegister =>
        !string.IsNullOrWhiteSpace(Reading) && !string.IsNullOrWhiteSpace(Word);

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private void Register()
    {
        var entry = new UserDictionaryEntry(Reading.Trim(), Word.Trim(), Pos, Comment.Trim());
        Registered?.Invoke(entry);
    }
}
