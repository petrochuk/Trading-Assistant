using AppCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace TradingAssistant;

public sealed partial class RiskGraph : UserControl
{
    #region Fields

    private readonly ILogger<RiskGraph> _logger;
    private SortedList<TimeSpan, Brush> _riskIntervals = new ();

    DispatcherTimer _drawRiskTimer = new DispatcherTimer() {
        Interval = TimeSpan.FromSeconds(15),
    };

    #endregion

    #region Constructors

    public RiskGraph() {
        InitializeComponent();

        _logger = AppCore.ServiceProvider.Instance.GetRequiredService<ILogger<RiskGraph>>();

        _riskIntervals.Add(TimeSpan.FromMinutes(5), (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"]);
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

        _drawRiskTimer.Tick += (s, args) => {
            Redraw();
        };
        _drawRiskTimer.Start();
    }

    #endregion

    public PositionsCollection? Positions { get; set; }

    private void OnSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e) {
        Redraw();
    }

    public void Redraw() {

        try {
            Canvas.Children.Clear();
            DrawBackground();

            UpdateGreeks();

            if (Positions == null || !Positions.Any()) {
                return;
            }

            DrawRiskIntervals();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error drawing risk graph");
        }
    }

    private void UpdateGreeks() {
        var delta = 0f;
        var gamma = 0f;
        var theta = 0f;
        var vega = 0f;

        foreach (var position in Positions!.Values) {
            if (position.UnderlyingSymbol != Positions.DefaultUnderlying?.UnderlyingSymbol) {
                continue;
            }
            if (position.AssetClass == AssetClass.Future || position.AssetClass == AssetClass.Stock) {
                delta += position.PositionSize;
            }
            else if (position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option) {
                if (position.Delta.HasValue) {
                    delta += position.Delta.Value * position.PositionSize;
                }
                if (position.Gamma.HasValue) {
                    gamma += position.Gamma.Value * position.PositionSize;
                }
                if (position.Theta.HasValue) {
                    theta += position.Theta.Value * position.PositionSize * position.Multiplier.Value;
                }
                if (position.Vega.HasValue) {
                    vega += position.Vega.Value * position.PositionSize;
                }
            }
        }

        DeltaText.Text = $"{delta.ToString("N2")}";
        ThetaText.Text = $"{theta.ToString("N2")}";
    }

    private void DrawRiskIntervals() {
        if (Positions == null || Positions.DefaultUnderlying == null) {
            return;
        }

        // First calculate the risk curves for each interval
        var midPrice = Positions.DefaultUnderlying.MarketPrice;
        var underlyingSymbol = Positions.DefaultUnderlying.UnderlyingSymbol;
        var minPrice = midPrice * 0.95f;
        var maxPrice = midPrice * 1.05f;
        var priceIncrement = (maxPrice - minPrice) / 100f;
        var riskCurves = new Dictionary<TimeSpan, RiskCurve>();
        var maxPL = float.MinValue;
        var minPL = float.MaxValue;
        foreach (var interval in _riskIntervals) {
            var riskCurve = CalculateRiskCurve(underlyingSymbol, interval.Key, minPrice, midPrice, maxPrice, priceIncrement);
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
            ((PathGeometry)path.Data).Figures.Add(pathFigure);
            for (var pointIdx = 1; pointIdx < points.Count; pointIdx++) {
                pathFigure.Segments.Add(new LineSegment() {
                    Point = new Point(MapX(points.GetKeyAtIndex(pointIdx), minPrice, maxPrice), MapY(points.GetValueAtIndex(pointIdx), minPL, maxPL)),
                });
            }
            Canvas.Children.Add(path);
        }

        DrawLabels(midPrice, minPrice, maxPrice, maxPL, minPL);
    }

    private void DrawLabels(float midPrice, float minPrice, float maxPrice, float maxPL, float minPL) {
        // Draw the min
        var minText = new TextBlock() {
            Text = minPL.ToString("C"),
            Foreground = (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"],
            FontSize = 12,
        };
        minText.Margin = new Thickness(minText.FontSize / 3);
        minText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
        Canvas.SetLeft(minText, MapX(midPrice, minPrice, maxPrice));
        Canvas.SetTop(minText, MapY(minPL, minPL, maxPL) - minText.ActualHeight - minText.Margin.Top - minText.Margin.Bottom);
        Canvas.Children.Add(minText);

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
        var maxText = new TextBlock() {
            Text = maxPL.ToString("C"),
            Foreground = (Brush)App.Current.Resources["ControlStrongFillColorDefaultBrush"],
            FontSize = 12,
        };
        maxText.Margin = new Thickness(maxText.FontSize / 3);
        Canvas.SetLeft(maxText, MapX(midPrice, minPrice, maxPrice));
        Canvas.SetTop(maxText, MapY(maxPL, minPL, maxPL));
        Canvas.Children.Add(maxText);
    }

    private double MapX(float value, float min, float max) {
        // Map the value to the width of the canvas
        var range = max - min;
        var mappedValue = (value - min) / range * ActualWidth;
        return mappedValue;
    }

    private double MapY(float value, float min, float max) {
        // Map the value to the height of the canvas
        var range = max - min;
        var mappedValue = (max - value) / range * ActualHeight;
        return mappedValue;
    }

    private RiskCurve CalculateRiskCurve(string underlyingSymbol, TimeSpan timeSpan, float minPrice, float midPrice, float maxPrice, float priceIncrement) {

        var riskCurve = new RiskCurve();

        // Estimate deltas with sigmoid function
        foreach (var position in Positions!.Values) {
            // Skip any positions that are not in the same underlying
            if (position.UnderlyingSymbol != underlyingSymbol) {
                continue;
            }
            else if (position.AssetClass == AssetClass.FutureOption) {
                if (position.Delta.HasValue) {
                    if (position.IsCall.Value)
                        position.DeltaEstimator = (float)Math.Log((1 - position.Delta.Value) / position.Delta.Value) / (position.Strike.Value - midPrice);
                    else
                        position.DeltaEstimator = (float)Math.Log(1 / (1 + position.Delta.Value) - 1) / (position.Strike.Value - midPrice);
                }
                else {
                    _logger.LogWarning($"Delta is not available for position {position.ContractDesciption}");
                }
            }
        }

        // Go through the price range and calculate the P&L for each position
        for (var currentPrice = minPrice; currentPrice < maxPrice; currentPrice += priceIncrement) {
            var totalPL = 0f;
            foreach (var position in Positions!.Values) {
                // Skip any positions that are not in the same underlying
                if (position.UnderlyingSymbol != underlyingSymbol) {
                    continue;
                }

                if (position.AssetClass == AssetClass.Future) {
                    totalPL += position.PositionSize * (currentPrice - position.MarketPrice) * position.Multiplier.Value;
                }
                else if (position.AssetClass == AssetClass.FutureOption ) {
                    // We need to add all values as delta changes from mid price
                    if (position.DeltaEstimator.HasValue) { 
                        var incrementDirection = currentPrice > midPrice ? 1 : -1;
                        var estimatedRange = Math.Abs(currentPrice - midPrice);
                        for (var deltaPrice = 0; deltaPrice < estimatedRange; deltaPrice += 1) {
                            var estimatedDelta = 1 / (1 + MathF.Exp(position.DeltaEstimator.Value * (position.Strike.Value - (midPrice + incrementDirection * deltaPrice))));
                            if (!position.IsCall.Value) {
                                estimatedDelta -= 1;
                            }
                            totalPL += estimatedDelta * incrementDirection * position.PositionSize * position.Multiplier.Value;
                        }
                    }
                }
            }
            riskCurve.Add(currentPrice, totalPL);
        }

        return riskCurve;
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
