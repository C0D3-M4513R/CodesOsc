// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.ObjectModel;
using System.Windows;
using OpenShock.SDK.CSharp.Models;
using VRCOSC.App.UI.Core;
using VRCOSC.App.Utils;

namespace CodeOSC.Modules.OpenShock.UI;

public partial class PhraseEditWindow : IManagedWindow
{
    private readonly OpenShockModule module;
    public Phrase Phrase { get; set; }

    public ObservableCollection<ShockerGroup> ShockerGroupsItemsSource => module.GroupsSetting.Attribute;
    public ControlType[] ModeItemsSource => Enum.GetValues<ControlType>();

    public PhraseEditWindow(OpenShockModule module, Phrase phrase)
    {
        InitializeComponent();

        this.module = module;
        Phrase = phrase;
        DataContext = this;

        phrase.Name.Subscribe(newName => Title = $"{newName.Pluralise()} Settings", true);
    }

    private void AddShockerGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        Phrase.ShockerGroups.Add(new Observable<Guid>(Guid.Empty));
    }

    private void RemoveShockerGroupButton_OnClick(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var instance = (Observable<Guid>)element.Tag;

        Phrase.ShockerGroups.Remove(instance);
    }

    public object GetComparer() => Phrase;
}