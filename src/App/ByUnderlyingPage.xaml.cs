using AppCore;
using Microsoft.UI.Xaml.Controls;

namespace TradingAssistant;

public sealed partial class ByUnderlyingPage : Page
{
    public ByUnderlyingPage() {
        InitializeComponent();
    }

    private PositionsCollection? _positions;

    public PositionsCollection? Positions {
        get => _positions;
        set => _positions = value;
    }

    private void UnderlyingList_SelectionChanged(object sender, SelectionChangedEventArgs e) {

    }
}
