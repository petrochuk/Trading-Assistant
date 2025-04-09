﻿using AppCore;
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
[JsonSerializable(typeof(Dictionary<string, List<Models.Contract>>))]
[JsonSerializable(typeof(List<Models.Contract>))]
[JsonSerializable(typeof(Models.Contract))]
[JsonSerializable(typeof(Models.SecurityDefinition))]
[JsonSerializable(typeof(List<Models.SecurityDefinition>))]
[JsonSerializable(typeof(Dictionary<string, List<Models.SecurityDefinition>>))]
[JsonSerializable(typeof(List<Responses.Account>))]
[JsonSerializable(typeof(Responses.Account))]
[JsonSerializable(typeof(Responses.AccountParent))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal partial class SourceGeneratorContext : JsonSerializerContext
{

}
