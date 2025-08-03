using System.Text.Json.Serialization;

namespace InteractiveBrokers.Models;

public class OrdersObject
{
    [JsonPropertyName("orders")]
    public required List<Order> Orders { get; set; } = [];
}

public class Order
{
    [JsonPropertyName("acctId")]
    public required string AccountId { get; set; }

    [JsonPropertyName("conid")]
    public int ContractId { get; set; }

    /// <summary>
    /// conidex is the identifier for the security and exchange you want to trade.
    /// Direct routed orders cannot use the conid field in addition to conidex, otherwise the order will be automatically routed to SMART.
    /// 265598@SMART
    /// </summary>
    [JsonPropertyName("conidex")]
    public required string ContractIdEx { get; set; }

    [JsonPropertyName("manualIndicator")]
    public bool ManualIndicator { get; set; } = false;

    /// <summary>
    /// The External Operator field should contain information regarding the submitting user in charge of the API operation at the time of request submission.
    /// </summary>
    [JsonPropertyName("extOperator")]
    public required string ExternalOperator { get; set; }

    /// <summary>
    /// The contract-identifier (conid) and security type (type) specified as a concatenated value format: "conid:type"
    /// </summary>
    [JsonPropertyName("secType")]
    public required string SecurityType { get; set; }

    /// <summary>
    /// Customer Order ID. An arbitrary string that can be used to identify the order. The value must be unique for a 24h span.
    /// Do not set this value for child orders when placing a bracket order.
    /// </summary>
    [JsonPropertyName("cOID")]
    public string? CustomerOrderId { get; set; }

    /// <summary>
    /// Only specify for child orders when placing bracket orders. 
    /// The parentId for the child order(s) must be equal to the cOId(customer order id) of the parent.
    /// </summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    /// <summary>
    /// The order-type determines what type of order you want to send. Available Order Types: LMT, MKT, STP, STOP_LIMIT, MIDPRICE, TRAIL, TRAILLMT
    /// </summary>
    [JsonPropertyName("orderType")]
    public required string OrderType { get; set; }

    /// <summary>
    /// Primary routing exchange for the order.
    /// </summary>
    [JsonPropertyName("listingExchange")]
    public string ListingExchange { get; set; } = "SMART";

    [JsonPropertyName("isSingleGroup")]
    public bool IsSingleGroup { get; set; } = false;

    [JsonPropertyName("outsideRTH")]
    public bool OutsideRTH { get; set; } = true;

    /// <summary>
    /// Required for LMT or STOP_LIMIT
    /// </summary>
    [JsonPropertyName("price")]
    public float? Price { get; set; }

    /// <summary>
    /// Required for STOP_LIMIT and TRAILLMT orders.
    /// </summary>
    [JsonPropertyName("auxPrice")]
    public float? AuxPrice { get; set; }

    /// <summary>
    /// Valid Values: SELL or BUY
    /// </summary>
    [JsonPropertyName("side")]
    public required string Side { get; set; }

    /// <summary>
    /// This is the underlying symbol for the contract.
    /// </summary>
    [JsonPropertyName("ticker")]
    public required string Ticker { get; set; }

    /// <summary>
    /// The Time-In-Force determines how long the order remains active on the market.
    /// Valid Values: GTC, OPG, DAY, IOC, PAX(CRYPTO ONLY).
    /// </summary>
    [JsonPropertyName("tif")]
    public string TimeInForce { get; set; } = "DAY";

    /// <summary>
    /// Required for TRAIL and TRAILLMT order
    /// </summary>
    [JsonPropertyName("trailingAmt")]
    public float? TrailingAmt { get; set; }

    /// <summary>
    /// Required for TRAIL and TRAILLMT order
    /// </summary>
    [JsonPropertyName("trailingType")]
    public string? TrailingType { get; set; }

    /// <summary>
    /// Custom order reference
    /// </summary>
    [JsonPropertyName("referrer")]
    public string? Referrer { get; set; }

    /// <summary>
    /// Used to designate the total number of shares traded for the order. Only whole share values are supported.
    /// </summary>
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("useAdaptive")]
    public bool UseAdaptive { get; set; }

    [JsonPropertyName("isCcyConv")]
    public bool IsCcyConv { get; set; }

    [JsonPropertyName("strategy")]
    public string? Strategy { get; set; }

    [JsonPropertyName("strategyParameters")]
    public StrategyParameters[] StrategyParameters { get; set; } = [];
}

public class StrategyParameters
{
    [JsonPropertyName("MaxPctVol")]
    public string? MaxPctVol { get; set; }

    [JsonPropertyName("StartTime")] 
    public string? StartTime { get; set; }

    [JsonPropertyName("EndTime")]
    public string? EndTime { get; set; }

    [JsonPropertyName("AllowPastEndTime")]
    public bool AllowPastEndTime { get; set; }
}
