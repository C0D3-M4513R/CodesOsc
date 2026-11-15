// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.App.SDK.Modules.Attributes.Settings;

namespace CodeOSC.Modules.OpenShock.UI;

public partial class OpenShockAPIKey
{
    public OpenShockAPIKey(OpenShockModule _, StringModuleSetting moduleSetting)
    {
        InitializeComponent();

        DataContext = moduleSetting;
    }
}
