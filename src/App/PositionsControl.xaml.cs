using AppCore;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Linq;

namespace TradingAssistant;

public sealed partial class PositionsControl : UserControl
{
    public PositionsControl() {
        InitializeComponent();
    }

    private void NavigationView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) {
        NavigationView.SelectedItem = NavigationView.MenuItems.FirstOrDefault();
        NavigationView_Navigate(string.Empty);
    }

    private PositionsCollection? _positions;

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public PositionsCollection? Positions {
        get => _positions;
        set => _positions = value;
    }

    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args) {
        var item = sender.MenuItems.OfType<NavigationViewItem>().First(x => (string)x.Content == (string)args.InvokedItem);
        NavigationView_Navigate((string)item.Tag);
    }

    private void NavigationView_Navigate(string itemTag) {
        ContentFrame.Navigate(typeof(ByUnderlyingPage));
        var page = ContentFrame.Content as ByUnderlyingPage;

        if (page != null) {
            page.Positions = _positions;
        }
    }
}
