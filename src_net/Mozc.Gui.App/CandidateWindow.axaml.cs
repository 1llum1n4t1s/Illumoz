using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Mozc.Gui.App;

public partial class CandidateWindow : Window
{
    public CandidateWindow() => AvaloniaXamlLoader.Load(this);
}
