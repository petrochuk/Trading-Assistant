using AppCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveBrokers;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)]
[JsonSerializable(typeof(Responses.Tickle))]
[JsonSerializable(typeof(Responses.IBServer))]
[JsonSerializable(typeof(Responses.HMDS))]
[JsonSerializable(typeof(Responses.AuthStatus))]
[JsonSerializable(typeof(Responses.ServerInfo))]
[JsonSerializable(typeof(List<Position>))]
[JsonSerializable(typeof(Position))]
[JsonSerializable(typeof(DisplayRule))]
[JsonSerializable(typeof(DisplayRule))]
[JsonSerializable(typeof(DisplayRuleStep))]
[JsonSerializable(typeof(IncrementRule))]
[JsonSerializable(typeof(List<Responses.Account>))]
[JsonSerializable(typeof(Responses.Account))]
[JsonSerializable(typeof(Responses.AccountParent))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal partial class SourceGeneratorContext : JsonSerializerContext
{

}
