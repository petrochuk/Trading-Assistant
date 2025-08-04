using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

public class PlaceOrder
{
    public string? order_id { get; set; }
    public string? order_status { get; set; }
    public string? encrypt_message { get; set; }
    public string? id { get; set; }

    [JsonPropertyName("message")]
    public List<string> Messages { get; set; } = [];

    public bool isSuppressed { get; set; }

    [JsonPropertyName("messageIds")]
    public List<string> MessageIds { get; set; } = [];
}
