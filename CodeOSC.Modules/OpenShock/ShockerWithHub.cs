using System;
using Newtonsoft.Json;
using OpenShock.SDK.CSharp.Models;
using OpenShock.SDK.CSharp.Serialization;

namespace CodeOSC.Modules.OpenShock;

[JsonObject(MemberSerialization.OptOut)]
public class ShockerWithHub : IEquatable<ShockerWithHub>
{
    [JsonProperty("hub")]
    public required Hub Hub;

    [JsonProperty("shocker")]
    public required Shocker Shocker;

    [JsonIgnore]
    public string Name => $"Hub: '{Hub.Name}', Shocker: '{Shocker.Name}'";

    [JsonIgnore]
    public string ShockerName => Shocker.Name;

    [JsonIgnore]
    public Guid ShockerId => Shocker.Id;

    public bool Equals(ShockerWithHub? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Hub == other.Hub && Shocker == other.Shocker;
    }
}

[JsonObject(MemberSerialization.OptOut)]
public class Hub
{
    [JsonProperty("id")]
    public required Guid Id { get; set; }

    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("created_on")]
    public required DateTime CreatedOn { get; set; }

    public static implicit operator Hub(ResponseHub hub)
    {
        return new Hub
        {
            Id = hub.Id,
            Name = hub.Name,
            CreatedOn = hub.CreatedOn,
        };
    }

    public static implicit operator ResponseHub(Hub hub)
    {
        return new ResponseHub
        {
            Id = hub.Id,
            Name = hub.Name,
            CreatedOn = hub.CreatedOn,
        };
    }
}

[JsonObject(MemberSerialization.OptOut)]
public class Shocker
{
    [JsonProperty("id")]
    public required Guid Id { get; set; }

    [JsonProperty("rfid")]
    public required ushort RfId { get; set; }

    [JsonProperty("model")]
    [System.Text.Json.Serialization.JsonConverter(typeof(UnknownEnumTypeConverter<ShockerModelType>))]
    public required ShockerModelType Model { get; set; }

    [JsonProperty("name")]
    public required string Name { get; set; }

    [JsonProperty("is_paused")]
    public required bool IsPaused { get; set; }

    [JsonProperty("created_on")]
    public required DateTime CreatedOn { get; set; }

    public static implicit operator Shocker(ShockerResponse shocker)
    {
        return new Shocker
        {
            Id = shocker.Id,
            RfId = shocker.RfId,
            Model = shocker.Model,
            Name = shocker.Name,
            IsPaused = shocker.IsPaused,
            CreatedOn = shocker.CreatedOn,
        };
    }

    public static implicit operator ShockerResponse(Shocker shocker)
    {
        return new ShockerResponse
        {
            Id = shocker.Id,
            RfId = shocker.RfId,
            Model = shocker.Model,
            Name = shocker.Name,
            IsPaused = shocker.IsPaused,
            CreatedOn = shocker.CreatedOn,
        };
    }
}