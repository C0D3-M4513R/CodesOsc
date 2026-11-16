// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Windows;
using VRCOSC.App.UI.Core;
using VRCOSC.App.Utils;

namespace CodeOSC.Modules.OpenShock.UI;

public partial class ShockerGroupEditWindow : IManagedWindow
{
    private readonly OpenShockModule module;
    public ShockerGroup ShockerGroup { get; set; }

    public ShockerCacheSetting ShockerItemsSource => module.ShockerWithHubs;

    public ShockerGroupEditWindow(OpenShockModule module, ShockerGroup shockerGroup)
    {
        InitializeComponent();

        this.module = module;
        ShockerGroup = shockerGroup;
        DataContext = this;

        shockerGroup.Name.Subscribe(newName => Title = $"{newName.Pluralise()} Settings", true);
    }

    private void AddShockerButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShockerGroup.Shockers.Add(new Observable<Guid>());
    }

    private void RemoveShockerButton_OnClick(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var instance = (Observable<Guid>)element.Tag;

        ShockerGroup.Shockers.Remove(instance);
    }

    public object GetComparer() => ShockerGroup;
}