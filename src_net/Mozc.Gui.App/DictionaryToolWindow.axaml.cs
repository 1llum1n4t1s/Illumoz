using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mozc.Gui.App;

public partial class DictionaryToolWindow : Window
{
    public DictionaryToolWindow() => AvaloniaXamlLoader.Load(this);
}
