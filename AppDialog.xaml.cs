using System.Windows;

namespace GlyphEcho;

public partial class AppDialog : Window
{
    private readonly bool _materialEnabled;

    private AppDialog(Window? owner, string title, string message, bool confirmation, bool danger)
    {
        InitializeComponent();
        Owner = owner;
        if (owner is null) WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        CancelButton.Visibility = confirmation ? Visibility.Visible : Visibility.Collapsed;
        AcceptButton.Content = confirmation ? "确认" : "知道了";
        if (danger) AcceptButton.Background = (System.Windows.Media.Brush)FindResource("DangerBrush");
        _materialEnabled = App.Settings.EnableMaterial;
    }

    internal static bool ShowMessage(Window? owner, string title, string message, bool confirmation = false, bool danger = false) =>
        new AppDialog(owner, title, message, confirmation, danger).ShowDialog() == true;

    internal static AppDialog CreateForVisualQa(Window owner, string title, string message) =>
        new(owner, title, message, true, true) { ShowActivated = false };

    private void Window_SourceInitialized(object? sender, EventArgs e) => NativeMethods.ApplyBackdrop(this, RootSurface, _materialEnabled);
    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
