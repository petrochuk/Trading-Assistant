using AppCore;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.Generic;
using System.Linq;

namespace TradingAssistant;

public sealed partial class RiskGraph : UserControl
{
    private SortedList<TimeSpan, Brush> _riskIntervals = new ();

    public RiskGraph() {
        InitializeComponent();

        _riskIntervals.Add(TimeSpan.FromMinutes(5), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        /* TODO
        _riskIntervals.Add(TimeSpan.FromMinutes(15), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromMinutes(30), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromHours(1), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromHours(2), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromHours(3), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromHours(6), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromHours(12), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromDays(1), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromDays(2), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        _riskIntervals.Add(TimeSpan.FromDays(3), (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"]);
        */
    }

    public PositionsCollection? Positions { get; set; }

    private void OnSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e) {
        Redraw();
    }

    public void Redraw() {
        Canvas.Children.Clear();
        DrawBackground();

        if (Positions == null || !Positions.Any()) {
            return;
        }

        DrawRiskIntervals();
    }

    private void DrawRiskIntervals() {
        foreach (var interval in _riskIntervals) {
            DrawRiskInterval(interval.Key, interval.Value);
        }
    }

    private void DrawRiskInterval(TimeSpan timeSpan, Brush brush) {
    }

    private void DrawBackground() {
        var topRect = new Rectangle() {
            Fill = (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"],
            Width = ActualWidth,
            Height = MidPixel(ActualHeight),
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Bottom,
        };
        Canvas.SetLeft(topRect, 0);
        Canvas.SetTop(topRect, 0);
        Canvas.Children.Add(topRect);

        var bottomRect = new Rectangle() {
            Fill = (Brush)App.Current.Resources["SystemFillColorCriticalBackgroundBrush"],
            Width = ActualWidth,
            Height = MidPixel(ActualHeight),
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Bottom,
        };
        Canvas.SetLeft(bottomRect, 0);
        Canvas.SetTop(bottomRect, MidPixel(ActualHeight));
        Canvas.Children.Add(bottomRect);

        var midLine = new Line() {
            // Get brush color from the theme
            Stroke = (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"],
            X1 = MidPixel(ActualWidth),
            X2 = MidPixel(ActualWidth),
            Y1 = 0,
            Y2 = ActualHeight,
        };
        Canvas.Children.Add(midLine);
    }

    private double MidPixel(double value) {
        return value % 2 == 0 ? value / 2 + 0.5 : value / 2;
    }
}
