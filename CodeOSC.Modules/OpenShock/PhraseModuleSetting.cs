// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OpenShock.SDK.CSharp.Models;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using CodeOSC.Modules.OpenShock.UI;

namespace CodeOSC.Modules.OpenShock;

public class PhraseModuleSetting : ListModuleSetting<Phrase>
{
    public PhraseModuleSetting()
        : base("Phrases", "Execute groups when a phrase is said", typeof(PhraseModuleSettingView), [])
    {
    }

    protected override Phrase CreateItem() => new();
}

[JsonObject(MemberSerialization.OptIn)]
public class Phrase : IEquatable<Phrase>
{
    public Guid ID = Guid.NewGuid();

    [JsonProperty("name")]
    public Observable<string> Name { get; } = new("New Phrase");

    [JsonProperty("use_regex")]
    public Observable<bool> UseRegex { get; } = new(false);

    [JsonProperty("text")]
    public Observable<string> Text { get; } = new(string.Empty);

    private Regex? textRegex;

    public bool Matches(string text)
    {
        if (!UseRegex.Value) return Text.Value == text;
        if (textRegex is not null) return textRegex.IsMatch(text);

        textRegex = new Regex(Text.Value, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return textRegex.IsMatch(text);
    }

    [JsonProperty("mode")]
    public Observable<ControlType> Mode { get; } = new();

    [JsonProperty("duration")]
    public Observable<ushort> Duration { get; } = new(15);

    [JsonProperty("intensity")]
    public Observable<byte> Intensity { get; } = new(100);

    [JsonProperty("shocker_groups")]
    public ObservableCollection<Observable<Guid>> ShockerGroups { get; } = [];

    [JsonConstructor]
    public Phrase()
    {
        Text.Subscribe(() => textRegex = null);
    }

    public bool Equals(Phrase? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name.Equals(other.Name) && Text.Equals(other.Text) && Mode.Equals(other.Mode) && ShockerGroups.SequenceEqual(other.ShockerGroups) && Duration.Equals(other.Duration) && Intensity.Equals(other.Intensity);
    }

    public override bool Equals(object? obj) => obj is Phrase phrase && Equals(phrase);
}