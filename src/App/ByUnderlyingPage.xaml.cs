using AppCore;
using AppCore.Models;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace TradingAssistant;

public sealed partial class ByUnderlyingPage : Page
{
    public ByUnderlyingPage() {
        InitializeComponent();
    }

    private PositionsCollection? _positions;

    public PositionsCollection? Positions {
        get => _positions;
        set {
            if (_positions != value) {
                _positions = value;
            }
        }
    }

    private void UnderlyingList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
    //    if (_positions != null && UnderlyingList.SelectedItem is UnderlyingPosition selectedPosition) {
      //      _positions.SelectedPosition = selectedPosition;
        //}
    }
}
