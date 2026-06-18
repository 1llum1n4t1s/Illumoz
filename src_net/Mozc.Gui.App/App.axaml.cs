using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Mozc.Gui;

namespace Mozc.Gui.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // --mode= で起動ダイアログを切り替える(C++ mozc_tool --mode= 相当)。
            string mode = GetMode(desktop.Args);
            desktop.MainWindow = mode switch
            {
                "config_dialog" => new ConfigWindow { DataContext = new ConfigViewModel() },
                "dictionary_tool" => new DictionaryToolWindow { DataContext = new DictionaryToolViewModel() },
                _ => new WordRegisterWindow { DataContext = new WordRegisterViewModel() },
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static string GetMode(string[]? args)
    {
        if (args == null)
        {
            return "word_register";
        }
        foreach (string a in args)
        {
            if (a.StartsWith("--mode=", global::System.StringComparison.Ordinal))
            {
                return a.Substring("--mode=".Length);
            }
        }
        return "word_register";
    }
}
