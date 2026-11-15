// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using CodeOSC.Modules.OpenShock.UI;

namespace CodeOSC.Modules.OpenShock;

public class ShockerGroupModuleSetting : ListModuleSetting<ShockerGroup>
{
    public ShockerGroupModuleSetting()
        : base("Groups", "Create, edit, and delete shocker groups\nThe left number is the group index for the group parameter and wildcard parameters", typeof(ShockerGroupModuleSettingView), [])
    {
    }

    protected override ShockerGroup CreateItem() => new();
}

[JsonObject(MemberSerialization.OptIn)]
public class ShockerGroup : IEquatable<ShockerGroup>
{
    [JsonProperty("id")]
    public Guid ID { get; set; } = Guid.NewGuid();

    [JsonProperty("name")]
    public Observable<string> Name { get; set; } = new("My New Group");

    [JsonProperty("shockers")]
    public ObservableCollection<Observable<Guid>> Shockers { get; set; } = new();

    [JsonProperty("max_duration")]
    [Range(300, 30000)]
    public Observable<ushort> MaxDuration { get; set; } = new(30000);

    [JsonProperty("max_intensity")]
    [Range(0, 100)]
    public Observable<byte> MaxIntensity { get; set; } = new(100);

    [JsonConstructor]
    public ShockerGroup()
    {
    }

    public bool Equals(ShockerGroup? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name.Equals(other.Name) && Shockers.SequenceEqual(other.Shockers) && MaxDuration.Equals(other.MaxDuration) && MaxIntensity.Equals(other.MaxIntensity);
    }
}