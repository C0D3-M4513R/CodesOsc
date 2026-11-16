using System;
using System.Collections.Immutable;
using CodeOSC.Modules.OpenShock.UI;
using OpenShock.SDK.CSharp.Models;
using VRCOSC.App.SDK.Modules.Attributes.Settings;

namespace CodeOSC.Modules.OpenShock;

public class ShockerCacheSetting : ListModuleSetting<ShockerWithHub>
{
    public ShockerCacheSetting()
        : base("Shocker Cache", "Cached Shockers from a specific Server and API-Key\nUpdates, when Modules are Ran next (which is typically on the Next VRChat start, but can be done manually)", typeof(ShockerCacheView), ImmutableArray<ShockerWithHub>.Empty)
    {
    }

    protected override ShockerWithHub CreateItem() => new()
    {
        Hub = new Hub
        {
            Id = Guid.Empty,
            Name = "",
            CreatedOn = default
        },
        Shocker = new Shocker
        {
            Id = Guid.Empty,
            RfId = 0,
            Model = ShockerModelType.CaiXianlin,
            Name = "",
            IsPaused = false,
            CreatedOn = default
        }
    };
}