namespace AppCore.Configuration;

public class ContractConfiguration
{
    public string Symbol { get; set; } = string.Empty;

    public float LongTermVolatility { get; set; } = 0.2f;

    public float VolatilityMeanReversion { get; set; } = 10f;

    public float VolatilityOfVolatility { get; set; } = 1f;

    public float Correlation { get; set; } = -1f;

    public float VarianceGammaDrift { get; set; } = -0.1f;

    public string OHLCHistoryFilePath { get; set; } = string.Empty;
}
