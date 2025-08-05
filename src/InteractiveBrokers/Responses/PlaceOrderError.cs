using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

public class PlaceOrderError
{
    public string Error { get; set; }
    public Cqe Cqe { get; set; }
    public string Action { get; set; }
}

public class Cqe
{
    public Post_Payload post_payload { get; set; }
    public string request_method { get; set; }
}

public class Post_Payload
{
    public Snapshot[] snapshots { get; set; }
    public string side { get; set; }
    public string[] rejections { get; set; }
    public string account_id { get; set; }
    public string sec_type { get; set; }
    public string[] clams_tags { get; set; }
    public string order_total { get; set; }
    public string conid { get; set; }
    public string exchange { get; set; }
    public string order_id { get; set; }
}

public class Snapshot
{
    public Balances balances { get; set; }
}

public class Balances
{
    public string USD { get; set; }
    public string BASE { get; set; }
}
