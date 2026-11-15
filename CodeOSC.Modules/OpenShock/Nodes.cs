// Copyright (c) VolcanicArts. Licensed under the LGPL License.
// See the LICENSE file in the repository root for full license text.

using System.Threading.Tasks;
using OpenShock.SDK.CSharp.Models;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Nodes;

namespace CodeOSC.Modules.OpenShock;

[Node("OpenShock Execute Group")]
public sealed class OpenShockExecuteGroupNode : ModuleNode<OpenShockModule>, IFlowInput
{
    public FlowContinuation OnSuccess = new("On Success");
    public FlowContinuation OnFail = new("On Fail");

    public ValueInput<int> Group = new();
    public ValueInput<ControlType> Mode = new();
    public ValueInput<float> Intensity = new();
    public ValueInput<int> Duration = new();
    public ValueInput<string?> CustomName = new();
    public ValueInput<bool> Exclusive = new();

    protected override async Task Process(PulseContext c)
    {
        var groupIndex = Group.Read(c);

        if (groupIndex < 0 || groupIndex >= Module.GroupsSetting.Attribute.Count)
        {
            await OnFail.Execute(c);
            return;
        }

        var group = Module.GroupsSetting.Attribute[groupIndex];

        if (group is null)
        {
            await OnFail.Execute(c);
            return;
        }

        var mode = Mode.Read(c);
        var intensity = float.Clamp(Intensity.Read(c), 0f, 1f);

        var result = await Module.ExecuteGroupAsyncFloat(group.ID, mode, intensity, Duration.Read(c), CustomName.Read(c), Exclusive.Read(c));

        if (!result)
        {
            await OnFail.Execute(c);
            return;
        }

        await OnSuccess.Execute(c);
    }
}
