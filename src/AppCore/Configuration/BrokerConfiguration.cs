namespace AppCore.Configuration;

public class BrokerConfiguration
{
    public string HostName { get; set; } = string.Empty;

    public string APIOperator { get; set; } = "Trading-Assistant";

    public Dictionary<string, string> Suppressions { get; set; } = [];
}
