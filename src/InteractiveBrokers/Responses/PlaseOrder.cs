namespace InteractiveBrokers.Responses;

public class PlaceOrder
{
    public string? order_id { get; set; }
    public string? order_status { get; set; }
    public string? encrypt_message { get; set; }
    public string? id { get; set; }
    public string[]? message { get; set; }
    public bool isSuppressed { get; set; }
    public string[]? messageIds { get; set; }
}
