using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mozc.Gui.App;

public partial class ConfigWindow : Window
{
    public ConfigWindow() => AvaloniaXamlLoader.Load(this);
}
