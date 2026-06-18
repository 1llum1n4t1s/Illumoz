using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mozc.Gui;

// C++ src/gui/dictionary_tool 相当の ViewModel(辞書ツール)。
// ユーザー辞書エントリの一覧管理(追加/削除/選択)。永続化は呼び出し側(UserDictionaryStore)。
// DataGrid 仮想化や TSV/MSIME インポートは View/後続。
public sealed partial class DictionaryToolViewModel : ObservableObject
{
    public ObservableCollection<UserDictionaryEntry> Entries { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private UserDictionaryEntry? _selectedEntry;

    public int Count => Entries.Count;

    public void Add(UserDictionaryEntry entry)
    {
        Entries.Add(entry);
        OnPropertyChanged(nameof(Count));
    }

    public bool CanDeleteSelected => SelectedEntry != null;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private void DeleteSelected()
    {
        if (SelectedEntry != null)
        {
            Entries.Remove(SelectedEntry);
            SelectedEntry = null;
            OnPropertyChanged(nameof(Count));
        }
    }

    // 部分一致検索(読み or 単語にマッチ)。
    public IEnumerable<UserDictionaryEntry> Find(string query)
    {
        foreach (UserDictionaryEntry e in Entries)
        {
            if (e.Key.Contains(query, global::System.StringComparison.Ordinal)
                || e.Value.Contains(query, global::System.StringComparison.Ordinal))
            {
                yield return e;
            }
        }
    }

    // TSV(key\tvalue\tpos[\tcomment])を取り込む(C++ import 相当の最小)。
    public int ImportTsv(string tsv)
    {
        int added = 0;
        foreach (string raw in tsv.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }
            string[] f = line.Split('\t');
            if (f.Length < 3)
            {
                continue;
            }
            Add(new UserDictionaryEntry(f[0], f[1], f[2], f.Length >= 4 ? f[3] : string.Empty));
            added++;
        }
        return added;
    }

    // 全エントリを TSV へ書き出す(C++ export 相当)。backend(UserDictionaryStorage)とは
    // この TSV 文字列を介して受け渡すため、型結合せずに往復できる。
    public string ExportTsv()
    {
        var sb = new global::System.Text.StringBuilder();
        foreach (UserDictionaryEntry e in Entries)
        {
            sb.Append(e.Key).Append('\t').Append(e.Value).Append('\t')
              .Append(e.Pos).Append('\t').Append(e.Comment).Append('\n');
        }
        return sb.ToString();
    }
}
