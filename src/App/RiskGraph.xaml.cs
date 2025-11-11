#region using
using AppCore;
using AppCore.Extenstions;
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
using TradingAssistant.Extensions;
using Windows.Foundation;
using Windows.UI;
#endregion

namespace TradingAssistant;

public sealed partial class RiskGraph : UserControl
{
    #region Fields

    private readonly ILogger<RiskGraph> _logger;
    private List<RiskCurveUI> _riskCurves = new ();

    DispatcherTimer _drawRiskTimer = new () {
        Interval = TimeSpan.FromSeconds(15),
    };

    class RiskCurveUI {
        public RiskCurve RiskCurve { get; set; }
        public Brush Brush { get; set; }

        public RiskCurveUI(RiskCurve riskCurve, Brush brush) {
            RiskCurve = riskCurve;
            Brush = brush;
        }
    }

    private readonly RiskCurve _expirationCurve;

    #endregion

    #region Constructors

    public RiskGraph() {
        InitializeComponent();

        _logger = AppCore.ServiceProvider.Instance.GetRequiredService<ILogger<RiskGraph>>();

        var riskCurve = new RiskCurve() {
            Name = "5 Minute",
            TimeSpan = TimeSpan.FromMinutes(5),
            Color = 0xffffff,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "15 Minute",
            TimeSpan = TimeSpan.FromMinutes(15),
            Color = 0xf2f5f8,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "30 Minute",
            TimeSpan = TimeSpan.FromMinutes(30),
            Color = 0xe6edf2,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "1 Hour",
            TimeSpan = TimeSpan.FromHours(1),
            Color = 0xd9e7ee,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "2 Hour",
            TimeSpan = TimeSpan.FromHours(2),
            Color = 0xccdee7,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "3 Hour",
            TimeSpan = TimeSpan.FromHours(3),
            Color = 0xb3d9e0,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "6 Hour",
            TimeSpan = TimeSpan.FromHours(6),
            Color = 0x99d2db,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "12 Hour",
            TimeSpan = TimeSpan.FromHours(12),
            Color = 0x80c9d4,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "1 Day",
            TimeSpan = TimeSpan.FromDays(1),
            Color = 0xffc2cc,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "2 Day",
            TimeSpan = TimeSpan.FromDays(2),
            Color = 0xffa9c4,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));
        
        riskCurve = new RiskCurve() {
            Name = "3 Day",
            TimeSpan = TimeSpan.FromDays(3),
            Color = 0xffa1bf,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "4 Day",
            TimeSpan = TimeSpan.FromDays(4),
            Color = 0xffa1bf,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        riskCurve = new RiskCurve() {
            Name = "5 Day",
            TimeSpan = TimeSpan.FromDays(5),
            Color = 0xff91a2,
        };
        _riskCurves.Add(new RiskCurveUI(riskCurve, new SolidColorBrush(riskCurve.Color.ToColor())));

        _expirationCurve = new RiskCurve() {
            Name = "Expiration",
            TimeSpan = TimeSpan.FromDays(4),
            Color = 0x00ff00,
        };
        _riskCurves.Add(new RiskCurveUI(_expirationCurve, new SolidColorBrush(_expirationCurve.Color.ToColor())));

        _drawRiskTimer.Tick += (s, args) => {
            Redraw();
        };
        _drawRiskTimer.Start();
    }

    #endregion

    Account? _account;
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public Account? Account 
    { 
        get => _account;
        set {
            if (_account != value) {
                _account = value;
                Redraw();
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
        Redraw();
    }

    public void Redraw() {

        try {
            Canvas.Children.Clear();
            DrawBackground();

            UpdateGreeks();

            if (Account == null || !Account.Positions.Any()) {
                return;
            }

            DrawRiskIntervals();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error drawing risk graph");
        }
    }

    private void UpdateGreeks() {
        if (Account == null || Account.Positions.SelectedPosition == null) {
            ClearGreeks();
            return;
        }

        var selectedSymbol = Account.Positions.SelectedPosition.Symbol;
        bool useRealizedVol = true;
        float minIV = 0;
        Greeks? greeks;
        if (Account.DeltaHedgers.TryGetValue(selectedSymbol, out var hedger)) { 
            useRealizedVol = false;
            minIV = hedger.Configuration.MinIV;
            greeks = hedger.LastGreeks;
        }
        else
            greeks = Account.Positions!.CalculateGreeks(minIV, useRealizedVol: useRealizedVol);

        if (greeks == null) {
            ClearGreeks();
            return;
        }

        if (Account.Positions.SelectedPosition != null && Account.Positions.SelectedPosition.RealizedVol != null) {

            if (Account.Positions.SelectedPosition.RealizedVol.TryGetValue(out var rv)) {
                RVText.Text = rv.ToString("P2");
            }
        }

        IVLongText.Text = $"{(greeks.VarianceWeightedIVLong):P2}";
        IVShortText.Text = $"{(greeks.VarianceWeightedIVShort):P2}";
        DeltaTotalText.Text = $"{(greeks.DeltaTotal):N2}";
        DeltaHedgeText.Text = $"{(greeks.DeltaHedge):N2}";
        GammaText.Text = $"{greeks.Gamma:N4}";
        CharmText.Text = $"{greeks.Charm:N2}";
        VannaText.Text = $"{greeks.Vanna:N2}";
        if (Account != null && Account.NetLiquidationValue != 0) {
            VegaText.Text = $"{greeks.Vega / Account.NetLiquidationValue:P2}";
            ThetaText.Text = $"{greeks.Theta / Account.NetLiquidationValue:P2}";
        }
    }

    private void ClearGreeks() {
        IVLongText.Text = "-";
        RVText.Text = "-";
        IVShortText.Text = "-";
        DeltaTotalText.Text = "-";
        DeltaHedgeText.Text = "-";
        GammaText.Text = "-";
        CharmText.Text = "-";
        ThetaText.Text = "-";
        VegaText.Text = "-";
    }

    private void DrawRiskIntervals() {
        if (Account == null || !Account.Positions.Any() || Account.Positions.SelectedPosition == null || Account.Positions.SelectedPosition.FrontContract == null) {
            return;
        }

        // First calculate the risk curves for each interval
        var midPrice = Account.Positions.SelectedPosition.FrontContract.MarketPrice;
        if (!midPrice.HasValue) {
            _logger.LogTrace("No market price available for underlying");
            return;
        }

        // For now assume expiration is at 16:00
        var estNow = TimeProvider.System.EstNow();
        if (estNow.Hour >= 16 || estNow.IsHoliday(includeWeekend: true)) {
            estNow = estNow.AddBusinessDays(1);
        }
        var estExpiration = new DateTimeOffset(estNow.Year, estNow.Month, estNow.Day, 16, 0, 0, TimeSpan.FromHours(-5));
        _expirationCurve.TimeSpan = TimeSpan.FromDays(TimeProvider.System.EstNow().BusinessDaysTo(estExpiration));

        var underlyingSymbol = Account.Positions.SelectedPosition.Symbol;
        var minMove = 0.97f;
        var maxMove = 1.03f;
        var maxPL = float.MinValue;
        var minPL = float.MaxValue;
        foreach (var interval in _riskCurves) {
            interval.RiskCurve.Clear();
            var riskCurve = Account.Positions.CalculateRiskCurve(underlyingSymbol, interval.RiskCurve, minMove, maxMove, (maxMove - minMove) / 100f);
            if (riskCurve == null) {
                _logger.LogWarning($"Risk curve for interval {interval.RiskCurve.TimeSpan} is null");
                continue;
            }
            if (riskCurve.MaxPL > maxPL) {
                maxPL = riskCurve.MaxPL;
            }
            if (riskCurve.MinPL < minPL) {
                minPL = riskCurve.MinPL;
            }
        }

        // Make min and max equal to each other
        if (-maxPL < minPL)
            minPL = -maxPL;
        if (maxPL < -minPL)
            maxPL = -minPL;

        // Now draw the risk curves
        foreach (var riskCurve in _riskCurves) {
            var points = riskCurve.RiskCurve.Points;
            if (points.Count == 0) {
                _logger.LogDebug($"No points available for risk curve {riskCurve.RiskCurve.Points}");
                continue;
            }
            var path = new Path() {
                Stroke = riskCurve.Brush,
                StrokeThickness = 1,
                Data = new PathGeometry(),
            };

            var pathFigure = new PathFigure() {
                StartPoint = new Point(MapX(points.GetKeyAtIndex(0), minMove, maxMove), MapY(points.GetValueAtIndex(0), minPL, maxPL)),
            };
            if (double.IsNaN(pathFigure.StartPoint.X) || double.IsNaN(pathFigure.StartPoint.Y)) {
                _logger.LogWarning($"Invalid start point {points.GetKeyAtIndex(0)}, {points.GetValueAtIndex(0)}");
                continue;
            }
            ((PathGeometry)path.Data).Figures.Add(pathFigure);
            for (var pointIdx = 1; pointIdx < points.Count; pointIdx++) {
                var lineSegment = new LineSegment() {
                    Point = new Point(MapX(points.GetKeyAtIndex(pointIdx), minMove, maxMove), MapY(points.GetValueAtIndex(pointIdx), minPL, maxPL)),
                };
                if (double.IsNaN(lineSegment.Point.X) || double.IsNaN(lineSegment.Point.Y)) {
                    _logger.LogWarning($"Invalid line segment {points.GetKeyAtIndex(pointIdx)}, {points.GetValueAtIndex(pointIdx)}");
                    continue;
                }
                pathFigure.Segments.Add(lineSegment);
            }
            Canvas.Children.Add(path);
        }

        DrawLabels(1, minMove, maxMove, maxPL, minPL);
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
            Text = (midPrice - 1).ToString("N2"),
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
