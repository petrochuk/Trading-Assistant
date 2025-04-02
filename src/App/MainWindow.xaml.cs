using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Popups;

namespace TradingAssistant;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        AppWindow.SetIcon("Resources/icons8-bell-curve-office-xs.ico");

        // Set the background color of the title bar to system color for current theme
        AppWindow.TitleBar.BackgroundColor = (Windows.UI.Color)App.Current.Resources["SystemAccentColorDark3"];
    }

    #region Event Handlers

    private async void Play_Click(object sender, RoutedEventArgs e) {
        try {
            await App.Instance.IBClient.Connect();
        }
        catch (Exception ex) {
            var contentDialog = new ContentDialog() {
                XamlRoot = Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Connection Error",
                Content = ex.Message,
                PrimaryButtonText = "Ok",
                DefaultButton = ContentDialogButton.Primary,
            };

            await contentDialog.ShowAsync();
        }
    }

    #endregion
}
