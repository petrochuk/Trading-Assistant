#region using
using AppCore;
using AppCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
#endregion

namespace TradingAssistant;

public sealed partial class RiskGraph : UserControl
{
    #region Fields

    private readonly ILogger<RiskGraph> _logger;
    private SortedList<TimeSpan, Brush> _riskIntervals = new ();

    DispatcherTimer _drawRiskTimer = new () {
        Interval = TimeSpan.FromSeconds(15),
    };

    #endregion

    #region Constructors

    public RiskGraph() {
        InitializeComponent();

        _logger = AppCore.ServiceProvider.Instance.GetRequiredService<ILogger<RiskGraph>>();

        _riskIntervals.Add(TimeSpan.FromMinutes(5), new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff)));
        _riskIntervals.Add(TimeSpan.FromMinutes(15), new SolidColorBrush(Color.FromArgb(0xff, 0xf2, 0xf5, 0xf8)));
        _riskIntervals.Add(TimeSpan.FromMinutes(30), new SolidColorBrush(Color.FromArgb(0xff, 0xe6, 0xed, 0xf2)));
        _riskIntervals.Add(TimeSpan.FromHours(1), new SolidColorBrush(Color.FromArgb(0xff, 0xd9, 0xe7, 0xee)));
        _riskIntervals.Add(TimeSpan.FromHours(2), new SolidColorBrush(Color.FromArgb(0xff, 0xcc, 0xe1, 0xe7)));
        _riskIntervals.Add(TimeSpan.FromHours(3), new SolidColorBrush(Color.FromArgb(0xff, 0xb3, 0xd9, 0xe0)));
        _riskIntervals.Add(TimeSpan.FromHours(6), new SolidColorBrush(Color.FromArgb(0xff, 0x99, 0xd2, 0xdb)));
        _riskIntervals.Add(TimeSpan.FromHours(12), new SolidColorBrush(Color.FromArgb(0xff, 0x80, 0xc9, 0xd4)));
        _riskIntervals.Add(TimeSpan.FromDays(1), new SolidColorBrush(Color.FromArgb(0xff, 0x66, 0xc2, 0xcc)));
        _riskIntervals.Add(TimeSpan.FromDays(2), new SolidColorBrush(Color.FromArgb(0xff, 0x4c, 0xa9, 0xc4)));
        _riskIntervals.Add(TimeSpan.FromDays(3), new SolidColorBrush(Color.FromArgb(0xff, 0x34, 0xa1, 0xbf)));
        _riskIntervals.Add(TimeSpan.FromDays(4), new SolidColorBrush(Color.FromArgb(0xff, 0x34, 0xa1, 0xbf)));
        _riskIntervals.Add(TimeSpan.FromDays(5), new SolidColorBrush(Color.FromArgb(0xff, 0x28, 0x91, 0xa2)));

        _drawRiskTimer.Tick += (s, args) => {
            Redraw();
        };
        _drawRiskTimer.Start();
    }

    #endregion

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public Account? Account { get; set; }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
        Redraw();
    }

    public void Redraw() {

        try {
            Canvas.Children.Clear();
            DrawBackground();

            UpdateGreeks();

            if (Account == null || !Account.Positions.Any()) {
                _logger.LogWarning("No positions available for the active account.");
                return;
            }

            DrawRiskIntervals();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error drawing risk graph");
        }
    }

    private void UpdateGreeks() {
        if (Account == null) {
            DeltaText.Text = string.Empty;
            GammaText.Text = string.Empty;
            CharmText.Text = string.Empty;
            ThetaText.Text = string.Empty;
            return;
        }

        var greeks = Account.Positions!.CalculateGreeks();

        if (Account.Positions.DefaultUnderlying != null && Account.Positions.DefaultUnderlying.RealizedVol != null) {

            if (Account.Positions.DefaultUnderlying.RealizedVol.TryGetValue(out var rv)) {
                // Annualize RV
                var annualizalizedRV = rv * System.Math.Sqrt(365.0 * 24.0 * (60.0 / PositionsCollection.RealizedVolPeriod.TotalMinutes));
                RVText.Text = annualizalizedRV.ToString("P2");
            }
        }

        DeltaText.Text = $"{greeks.Delta:N2}";
        GammaText.Text = $"{greeks.Gamma:N4}";
        CharmText.Text = $"{greeks.Charm:N2}";
        if (Account != null && Account.NetLiquidationValue != 0) {
            ThetaText.Text = $"{greeks.Theta / Account.NetLiquidationValue:P2}";
        }
    }

    private void DrawRiskIntervals() {
        if (Account == null || !Account.Positions.Any() || Account.Positions.DefaultUnderlying == null) {
            return;
        }

        // First calculate the risk curves for each interval
        var midPrice = Account.Positions.DefaultUnderlying.MarketPrice;
        if (midPrice == 0) {
            _logger.LogTrace("No market price available for underlying");
            return;
        }

        var underlyingSymbol = Account.Positions.DefaultUnderlying.Symbol;
        var minPrice = midPrice * 0.95f;
        var maxPrice = midPrice * 1.05f;
        var priceIncrement = (maxPrice - minPrice) / 100f;
        var riskCurves = new Dictionary<TimeSpan, RiskCurve>();
        var maxPL = float.MinValue;
        var minPL = float.MaxValue;
        foreach (var interval in _riskIntervals) {
            var riskCurve = Account.Positions.CalculateRiskCurve(underlyingSymbol, interval.Key, minPrice, midPrice, maxPrice, priceIncrement);
            if (riskCurve.MaxPL > maxPL) {
                maxPL = riskCurve.MaxPL;
            }
            if (riskCurve.MinPL < minPL) {
                minPL = riskCurve.MinPL;
            }
            riskCurves.Add(interval.Key, riskCurve);
        }

        // Make min and max equal to each other
        if (-maxPL < minPL)
            minPL = -maxPL;
        if (maxPL < -minPL)
            maxPL = -minPL;

        // Now draw the risk curves
        foreach (var riskCurve in riskCurves) {
            var curve = riskCurve.Value;
            var points = curve.Points;
            var path = new Path() {
                Stroke = _riskIntervals[riskCurve.Key],
                StrokeThickness = 1,
                Data = new PathGeometry(),
            };

            var pathFigure = new PathFigure() {
                StartPoint = new Point(MapX(points.GetKeyAtIndex(0), minPrice, maxPrice), MapY(points.GetValueAtIndex(0), minPL, maxPL)),
            };
            if (double.IsNaN(pathFigure.StartPoint.X) || double.IsNaN(pathFigure.StartPoint.Y)) {
                _logger.LogWarning($"Invalid start point {points.GetKeyAtIndex(0)}, {points.GetValueAtIndex(0)}");
                continue;
            }
            ((PathGeometry)path.Data).Figures.Add(pathFigure);
            for (var pointIdx = 1; pointIdx < points.Count; pointIdx++) {
                var lineSegment = new LineSegment() {
                    Point = new Point(MapX(points.GetKeyAtIndex(pointIdx), minPrice, maxPrice), MapY(points.GetValueAtIndex(pointIdx), minPL, maxPL)),
                };
                if (double.IsNaN(lineSegment.Point.X) || double.IsNaN(lineSegment.Point.Y)) {
                    _logger.LogWarning($"Invalid line segment {points.GetKeyAtIndex(pointIdx)}, {points.GetValueAtIndex(pointIdx)}");
                    continue;
                }
                pathFigure.Segments.Add(lineSegment);
            }
            Canvas.Children.Add(path);
        }

        DrawLabels(midPrice, minPrice, maxPrice, maxPL, minPL);
    }

    private void DrawLabels(float midPrice, float minPrice, float maxPrice, float maxPL, float minPL) {
        // Draw the min
        if (Account != null && Account.NetLiquidationValue != 0) {
            var minText = new TextBlock() {
                Text = (minPL / Account.NetLiquidationValue).ToString("P2"),
                Foreground = (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"],
                FontSize = 12,
            };
            minText.Margin = new Thickness(minText.FontSize / 3);
            minText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            Canvas.SetLeft(minText, MapX(midPrice, minPrice, maxPrice));
            Canvas.SetTop(minText, MapY(minPL, minPL, maxPL) - minText.ActualHeight - minText.Margin.Top - minText.Margin.Bottom);
            Canvas.Children.Add(minText);
        }

        // Draw the mid
        var midText = new TextBlock() {
            Text = midPrice.ToString("N2"),
            Foreground = (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"],
            FontSize = 12,
        };
        midText.Margin = new Thickness(midText.FontSize / 3);
        midText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
        Canvas.SetLeft(midText, MapX(midPrice, minPrice, maxPrice));
        Canvas.SetTop(midText, MapY(0, minPL, maxPL));
        Canvas.Children.Add(midText);

        // Draw the max
        if (Account != null && Account.NetLiquidationValue != 0) {
            var maxText = new TextBlock() {
                Text = (maxPL / Account.NetLiquidationValue).ToString("P2"),
                Foreground = (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"],
                FontSize = 12,
            };
            maxText.Margin = new Thickness(maxText.FontSize / 3);
            Canvas.SetLeft(maxText, MapX(midPrice, minPrice, maxPrice));
            Canvas.SetTop(maxText, MapY(maxPL, minPL, maxPL));
            Canvas.Children.Add(maxText);
        }
    }

    private double MapX(float value, float min, float max) {
        // Map the value to the width of the canvas
        var range = max - min;
        var mappedValue = (value - min) / range * Canvas.ActualWidth;
        return mappedValue;
    }

    private double MapY(float value, float min, float max) {
        // Map the value to the height of the canvas
        var range = max - min;
        var mappedValue = (max - value) / range * Canvas.ActualHeight;
        return mappedValue;
    }

    private void DrawBackground() {
        var topRect = new Rectangle() {
            Fill = (Brush)App.Current.Resources["SystemFillColorSuccessBackgroundBrush"],
            Width = ActualWidth,
            Height = MidPixel(Canvas.ActualHeight),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Canvas.SetLeft(topRect, 0);
        Canvas.SetTop(topRect, 0);
        Canvas.Children.Add(topRect);

        var bottomRect = new Rectangle() {
            Fill = (Brush)App.Current.Resources["SystemFillColorCriticalBackgroundBrush"],
            Width = ActualWidth,
            Height = MidPixel(Canvas.ActualHeight),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Canvas.SetLeft(bottomRect, 0);
        Canvas.SetTop(bottomRect, MidPixel(Canvas.ActualHeight));
        Canvas.Children.Add(bottomRect);

        var midLine = new Line() {
            // Get brush color from the theme
            Stroke = (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"],
            X1 = MidPixel(ActualWidth),
            X2 = MidPixel(ActualWidth),
            Y1 = 0,
            Y2 = Canvas.ActualHeight,
        };
        Canvas.Children.Add(midLine);
    }

    private double MidPixel(double value) {
        return value % 2 == 0 ? value / 2 + 0.5 : value / 2;
    }
}
