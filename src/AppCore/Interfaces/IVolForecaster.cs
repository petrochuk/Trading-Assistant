namespace AppCore.Interfaces;

public interface IVolForecaster
{
    void CalibrateFromFile(string symbol, string filePath, int skipLines = 0);

    bool IsCalibrated { get; }

    void SetIntradayVolatilityEstimate(double volatility, bool isAnnualized = false, double? currentLogReturn = null);


    double Forecast(double forecastHorizonDays);

    string Symbol { get; set; }
}
