using AppCore.Extenstions;
using AppCore.Interfaces;
using AppCore.MachineLearning;
using System.Globalization;

namespace AppCore.Statistics;

public class VolMlModel : IVolForecaster
{
    private const double DefaultYangZhangK = 0.34 / (1.34 + (79.0 / 77.0));

    private const int InputSize = 9;
    private readonly List<DailyData> _returns = new();
    private Network _network = new Network(
        inputSize: InputSize,
        outputSize: MaxDaysAhead,
        hiddenLayers: 2,
        hiddenSize: 5 * InputSize,
        learningRate: 0.05,
        useLinearOutputLayer: false,
        hiddenActivation: Network.ActivationType.GELU,
        outputActivation: Network.ActivationType.Softplus);
    private List<(double[] inputs, double[] outputs)> _trainingData = new();

    record class DailyData(DateOnly Date, double dailyReturn, double dailyVariance);

    enum Inputs {
        /// <summary>
        /// Variance of returns over the last day
        /// </summary>
        Variance0 = 0,
        /// <summary>
        /// Total variance of returns for the last 2 days
        /// </summary>
        Variance1 = 1,
        /// <summary>
        /// Total variance of returns for the last 3 days
        /// </summary>
        Variance3 = 2,
        /// <summary>
        /// Total variance of returns for the last 4 days
        /// </summary>
        Variance4 = 3,
        /// <summary>
        /// Total variance of returns for the last 5 days
        /// </summary>
        Variance5 = 4,
        /// <summary>
        /// Total variance of returns for the last 10 days
        /// </summary>
        Variance10 = 5,
        /// <summary>
        /// Total variance of returns for the last 15 days
        /// </summary>
        Variance15 = 6,
        /// <summary>
        /// Total variance of returns for the last 25 days
        /// </summary>
        Variance25 = 7,
        /// <summary>
        /// Total variance of returns for the last 100 days
        /// </summary>
        Variance100 = 8,
    }

    const int VarianceLookbackDays = 5;
    const int MinDaysHistory = VarianceLookbackDays + 100;
    const int MaxDaysAhead = 20;

    public bool IsCalibrated => throw new NotImplementedException();

    private string? _symbol;
    public string Symbol
    {
        get => _symbol ?? string.Empty;
        set => _symbol = value;
    }

    public void Load(string dateFilePath, string networkFile, bool forTraining = false) {
        if (!File.Exists(dateFilePath))
            throw new FileNotFoundException("Input file not found.", dateFilePath);

        if (File.Exists(networkFile))
            _network = Network.Load(networkFile);
        _returns.Clear();

        double? previousPrice = null;
		int lineNumber = 0;
		int dateIdx = 0, openIdx = 1, highIdx = 2, lowIdx = 3, closeIdx = 4;

        foreach (var rawLine in File.ReadLines(dateFilePath)) {
            lineNumber++;

            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var colummns = line.Split([',', ';', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (colummns.Length < 5)
                continue;

            // Trim optional double quotes from each column
            for (int i = 0; i < colummns.Length; i++) {
                colummns[i] = colummns[i].Trim('"');
            }

            // Parse header
            if (lineNumber == 1) {
                for (int i = 0; i < colummns.Length; i++) {
                    switch (colummns[i].ToLowerInvariant()) {
                        case "date":
                            dateIdx = i;
                            break;
                        case "open":
                            openIdx = i;
                            break;
                        case "high":
                            highIdx = i;
                            break;
                        case "low":
                            lowIdx = i;
                            break;
                        case "price":
                        case "close":
                            closeIdx = i;
                            break;
                    }
                }
                continue;
            }

            // Parse date
            if (!DateOnly.TryParse(colummns[dateIdx], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new FormatException($"Unable to parse date on line {lineNumber}: '{rawLine}'.");

            if (!double.TryParse(colummns[closeIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var close))
                throw new FormatException($"Unable to parse numeric value on line {lineNumber}: '{rawLine}'.");


            if (!double.TryParse(colummns[openIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var open) ||
                !double.TryParse(colummns[highIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var high) ||
                !double.TryParse(colummns[lowIdx], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var low)) {
                throw new FormatException($"Unable to parse OHLC values on line {lineNumber}: '{rawLine}'.");
            }

            if (open <= 0 || high <= 0 || low <= 0 || close <= 0)
                throw new InvalidOperationException($"OHLC prices must be positive on line {lineNumber}.");

            if (high < low)
                throw new InvalidOperationException($"High must be greater than or equal to low on line {lineNumber}.");

            if (previousPrice.HasValue) {
                var closeToCloseReturn = System.Math.Log(close / previousPrice.Value);
                var realizedVariance = ComputeYangZhangVariance(previousPrice.Value, open, high, low, close);
                _returns.Add(new DailyData(date, closeToCloseReturn, realizedVariance));
            }

            previousPrice = close;
        }

        if (forTraining)
            PrepareTrainingData();
    }

    private void PrepareTrainingData() {
        _trainingData.Clear();

        for (int i = MinDaysHistory; i < _returns.Count - MaxDaysAhead; i++) {
            var inputs = new double[_network.InputSize];
            var daysBackVariance = 0.0;
            // Align with Forecast(): include current day i in the lookbacks.
            for (int j = 1; j <= VarianceLookbackDays; j++) {
                daysBackVariance = 0;
                for (int k = 0; k < j; k++) {
                    daysBackVariance += _returns[i - k].dailyVariance; // was i - 1 - k
                }
                inputs[(int)Inputs.Variance0 + j - 1] = System.Math.Log(1 + daysBackVariance);
            }

            // 10 days back variance (include current day i)
            daysBackVariance = 0.0;
            for (int j = 1; j <= 10; j++) {
                daysBackVariance += _returns[i - (j - 1)].dailyVariance; // was i - 1 - (j - 1)
            }
            inputs[(int)Inputs.Variance10] = System.Math.Log(1 + daysBackVariance);

            // 15 days back variance
            daysBackVariance = 0.0;
            for (int j = 1; j <= 15; j++) {
                daysBackVariance += _returns[i - (j - 1)].dailyVariance; // was i - 1 - (j - 1)
            }
            inputs[(int)Inputs.Variance15] = System.Math.Log(1 + daysBackVariance);

            // 25 days back variance
            daysBackVariance = 0.0;
            for (int j = 1; j <= 25; j++) {
                daysBackVariance += _returns[i - (j - 1)].dailyVariance; // was i - 1 - (j - 1)
            }
            inputs[(int)Inputs.Variance25] = System.Math.Log(1 + daysBackVariance);

            // 100 days back variance
            daysBackVariance = 0.0;
            for (int j = 1; j <= 100; j++) {
                daysBackVariance += _returns[i - (j - 1)].dailyVariance; // was i - 1 - (j - 1)
            }
            inputs[(int)Inputs.Variance100] = System.Math.Log(1 + daysBackVariance);

            // Outputs: forward cumulative variance starting AFTER current day (i+1)
            var outputs = new double[MaxDaysAhead];
            var daysAheadVariance = 0.0;
            for (int j = 0; j < MaxDaysAhead; j++) {
                daysAheadVariance += _returns[i + 1 + j].dailyVariance; // was i + j
                outputs[j] = System.Math.Log(1 + daysAheadVariance); // index j => cumulative variance for (j+1) days ahead
            }
            _trainingData.Add((inputs, outputs));
        }
    }

    public void Train(string networkFile) {
        if (_trainingData.Count == 0)
            throw new InvalidOperationException("No training data available. Load data with forTraining=true first.");

        var totalSquaredError = 0.0;
        var epoch = 0;
        var random = new Random();
        var indices = Enumerable.Range(0, _trainingData.Count).ToArray();
        while (true) {
            totalSquaredError = 0;
            ShuffleIndices(indices, random);

            for (int idx = 0; idx < indices.Length; idx++) {
                var sample = _trainingData[indices[idx]];
                var error = _network.Train(sample.inputs, sample.outputs, Network.LossFunction.QLIKE);
                totalSquaredError += error;
            }

            var avgError = totalSquaredError / _trainingData.Count;
            _network.AdjustLearningRate(avgError);
            epoch++;

            // Print progress every 10 epochs
            if (epoch % 10 == 0) {
                Console.WriteLine($"Epoch {epoch}, Average Error: {avgError}");
                try {
                    _network.Save(networkFile);
                }
                catch (IOException ex) {
                    Console.WriteLine($"Warning: Unable to save network to file: {ex.Message}");
                }
                if (Console.KeyAvailable) {
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.S) {
                        Console.WriteLine("Training interrupted by user.");
                        break;
                    }
                }
            }
        }

        Console.WriteLine($"Training completed. RMSE on training data: {ForecastingError():F6}");
    }

    public double ForecastingError() {
        var totalSquaredError = 0.0;
        int forecastCount = 0;
        for (int daysAhead = 1; daysAhead <= 20; daysAhead++) {
            for (int dayNumber = MinDaysHistory; dayNumber < _returns.Count - daysAhead; dayNumber++) {
                var forecastVol = Forecast(daysAhead, dayNumber);
                var actualVariance = 0.0;
                for (int j = 0; j < daysAhead; j++) {
                    actualVariance += _returns[dayNumber + 1 + j].dailyVariance;
                }
                var actualVol = System.Math.Sqrt(actualVariance) * System.Math.Sqrt(TimeExtensions.BusinessDaysPerYear / daysAhead);
                var error = forecastVol - actualVol;
                totalSquaredError += error * error;
                forecastCount++;
            }
        }

        return System.Math.Sqrt(totalSquaredError / forecastCount);
    }

    public double Forecast(double daysAhead, int dayNumber) {
        if (_returns.Count < MinDaysHistory)
            throw new InvalidOperationException("Not enough data for prediction.");
        if (dayNumber < MinDaysHistory || dayNumber >= _returns.Count)
            throw new ArgumentOutOfRangeException(nameof(dayNumber), $"Invalid day number for prediction: {dayNumber}");

        var inputs = new double[_network.InputSize];
        inputs[(int)Inputs.Variance0] = System.Math.Log(1 + _returns[dayNumber].dailyVariance);
        inputs[(int)Inputs.Variance1] = System.Math.Log(1 + _returns[dayNumber].dailyVariance + _returns[dayNumber - 1].dailyVariance);
        inputs[(int)Inputs.Variance3] = System.Math.Log(1 + _returns[dayNumber].dailyVariance + _returns[dayNumber - 1].dailyVariance + _returns[dayNumber - 2].dailyVariance);
        inputs[(int)Inputs.Variance4] = System.Math.Log(1 + _returns[dayNumber].dailyVariance + _returns[dayNumber - 1].dailyVariance + _returns[dayNumber - 2].dailyVariance + _returns[dayNumber - 3].dailyVariance);
        inputs[(int)Inputs.Variance5] = System.Math.Log(1 + _returns[dayNumber].dailyVariance + _returns[dayNumber - 1].dailyVariance + _returns[dayNumber - 2].dailyVariance + _returns[dayNumber - 3].dailyVariance + _returns[dayNumber - 4].dailyVariance);

        // Add 10 days back variance
        var daysBackVariance = 0.0;
        for (int j = 0; j < 10; j++) {
            daysBackVariance += _returns[dayNumber - j].dailyVariance;
        }
        inputs[(int)Inputs.Variance10] = System.Math.Log(1 + daysBackVariance);

        // Add 15 days back variance
        daysBackVariance = 0.0;
        for (int j = 0; j < 15; j++) {
            daysBackVariance += _returns[dayNumber - j].dailyVariance;
        }
        inputs[(int)Inputs.Variance15] = System.Math.Log(1 + daysBackVariance);

        // Add 25 days back variance
        daysBackVariance = 0.0;
        for (int j = 0; j < 25; j++) {
            daysBackVariance += _returns[dayNumber - j].dailyVariance;
        }
        inputs[(int)Inputs.Variance25] = System.Math.Log(1 + daysBackVariance);

        // Add 100 days back variance
        daysBackVariance = 0.0;
        for (int j = 0; j < 100; j++) {
            daysBackVariance += _returns[dayNumber - j].dailyVariance;
        }
        inputs[(int)Inputs.Variance100] = System.Math.Log(1 + daysBackVariance);

        var output = _network.Predict(inputs);

        // Interpret output as cumulative variance for (horizon) days ahead; index 0 => 1 day ahead
        var horizon = (int)System.Math.Clamp(System.Math.Round(daysAhead), 1, output.Length);
        var varianceForecast = System.Math.Max(0.0, output[horizon - 1]);

        // Convert log variance back to variance
        varianceForecast = System.Math.Exp(varianceForecast) - 1.0;

        // Return annualized volatility
        return System.Math.Sqrt(varianceForecast) * System.Math.Sqrt(TimeExtensions.BusinessDaysPerYear / horizon);
    }

    private static double ComputeYangZhangVariance(double previousClose, double open, double high, double low, double close) {
        var overnightReturn = System.Math.Log(open / previousClose);
        var intradayReturn = System.Math.Log(close / open);
        var logHighOpen = System.Math.Log(high / open);
        var logLowOpen = System.Math.Log(low / open);

        var rs = logHighOpen * (logHighOpen - intradayReturn) + logLowOpen * (logLowOpen - intradayReturn);
        var variance = overnightReturn * overnightReturn + DefaultYangZhangK * intradayReturn * intradayReturn + (1.0 - DefaultYangZhangK) * rs;

        return variance < 0 ? 0.0 : variance;
    }

    public void CalibrateFromFile(string symbol, string filePath, int skipLines = 0) {
        // For network model change file extension to .nn
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Input file not found.", filePath);

        var networkFile = Path.ChangeExtension(filePath, ".nn");

        Load(filePath, networkFile, forTraining: false);
    }

    public void SetIntradayVolatilityEstimate(double volatility, bool isAnnualized = false, double? currentLogReturn = null) {
        // Not used in this model
    }

    public double Forecast(double forecastHorizonDays) {
        return Forecast(forecastHorizonDays, _returns.Count - 1);
    }

    private static void ShuffleIndices(int[] indices, Random random) {
        for (int i = indices.Length - 1; i > 0; i--) {
            int j = random.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
    }
}
