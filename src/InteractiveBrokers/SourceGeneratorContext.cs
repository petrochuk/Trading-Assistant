using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveBrokers;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
    PropertyNameCaseInsensitive = true, 
    AllowTrailingCommas = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Responses.Tickle))]
[JsonSerializable(typeof(Responses.IBServer))]
[JsonSerializable(typeof(Responses.HMDS))]
[JsonSerializable(typeof(Responses.AuthStatus))]
[JsonSerializable(typeof(Responses.ServerInfo))]
[JsonSerializable(typeof(Responses.Authenticate))]
[JsonSerializable(typeof(Responses.SessionInit))]
[JsonSerializable(typeof(List<Responses.Position>))]
[JsonSerializable(typeof(Dictionary<string, List<Models.Contract>>))]
[JsonSerializable(typeof(List<Models.Contract>))]
[JsonSerializable(typeof(Models.Contract))]
[JsonSerializable(typeof(Models.Order))]
[JsonSerializable(typeof(Models.OrdersObject))]
[JsonSerializable(typeof(Models.SecurityDefinition))]
[JsonSerializable(typeof(List<Models.SecurityDefinition>))]
[JsonSerializable(typeof(Dictionary<string, List<Models.SecurityDefinition>>))]
[JsonSerializable(typeof(List<Responses.Account>))]
[JsonSerializable(typeof(Responses.Account))]
[JsonSerializable(typeof(Responses.Accounts))]
[JsonSerializable(typeof(Responses.AccountParent))]
[JsonSerializable(typeof(Responses.PlaceOrder))]
[JsonSerializable(typeof(Responses.PlaceOrderError))]
[JsonSerializable(typeof(Responses.SuppressWarnings))]
[JsonSerializable(typeof(List<Responses.PlaceOrder>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(AppCore.Args.AccountSummaryArgs))]
[JsonSerializable(typeof(AppCore.Args.SummaryLine))]
internal partial class SourceGeneratorContext : JsonSerializerContext
{
}
