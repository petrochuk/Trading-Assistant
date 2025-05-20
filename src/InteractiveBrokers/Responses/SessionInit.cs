using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

public class SessionInit
{
    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; set; }

    [JsonPropertyName("competing")]
    public bool Competing { get; set; }

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("passed")]
    public bool Passed { get; set; }

    [JsonPropertyName("hardware_info")]
    public string HardwareInfo { get; set; } = string.Empty;
}
