// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CodeOSC.Modules.OpenShock.UI;
using OpenShock.SDK.CSharp;
using OpenShock.SDK.CSharp.Models;
using VRCOSC.App.SDK.Handlers;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using VRCOSC.App.Utils;
using Module = VRCOSC.App.SDK.Modules.Module;

namespace CodeOSC.Modules.OpenShock;

[ModuleTitle("OpenShock")]
[ModuleDescription("Allows for controlling OpenShock shockers with avatar parameters and your voice")]
[ModuleType(ModuleType.NSFW)]
[ModulePrefab("Official Prefabs", "https://vrcosc.com/docs/downloads#prefabs")]
public class OpenShockModule : Module, ISpeechHandler
{
    private OpenShockApiClient openShockProvider = null!;
    private CancellationTokenSource cancellationToken = new CancellationTokenSource();

    private Guid? globalGroupID;
    private float globalDuration;
    private float globalIntensity;

    private readonly Dictionary<Guid, float> durations = new();
    private readonly Dictionary<Guid, float> intensities = new();

    private (DateTimeOffset, Guid?)? shock;
    private (DateTimeOffset, Guid?)? vibrate;
    private (DateTimeOffset, Guid?)? beep;
    private bool shockExecuted;
    private bool vibrateExecuted;
    private bool beepExecuted;

    private readonly List<Guid> executedPhrases = [];

    public ShockerCacheSetting ShockerWithHubs => GetSetting<ShockerCacheSetting>(OpenShockSetting.ShockerCache);
    public ShockerGroupModuleSetting GroupsSetting => GetSetting<ShockerGroupModuleSetting>(OpenShockSetting.Groups);

    protected override void OnPreLoad()
    {
        CreateTextBox(OpenShockSetting.Server, "Server", "The OpenShock Server", "https://api.openshock.app");
        CreateCustomSetting(OpenShockSetting.APIKey, new StringModuleSetting("API Key", "Your OpenShock API key", typeof(OpenShockAPIKey), string.Empty));
        CreateCustomSetting(OpenShockSetting.ShockerCache, new ShockerCacheSetting());

        CreateSlider(OpenShockSetting.ButtonDelay, "Button Delay",
            "The amount of time in milliseconds the shock, vibrate, and beep parameters need to be true to execute the action. This is helpful for if you accidentally press buttons on your action menu", 0, 0, 1000, 10);

        CreateCustomSetting(OpenShockSetting.Groups, new ShockerGroupModuleSetting());
        CreateCustomSetting(OpenShockSetting.Phrases, new PhraseModuleSetting());
        CreateToggle(OpenShockSetting.PrintAllText, "Print All Text", "Print All recognised Text", false);
        CreateToggle(OpenShockSetting.PrintMatchedText, "Print Matched Text", "Print Text, where at least one match was found", false);

        CreateGroup("Credentials", string.Empty, OpenShockSetting.Server, OpenShockSetting.APIKey, OpenShockSetting.ShockerCache);
        CreateGroup("Management", string.Empty, OpenShockSetting.Groups);
        CreateGroup("Tweaks", string.Empty, OpenShockSetting.ButtonDelay);
        CreateGroup("Speech", string.Empty, OpenShockSetting.Phrases, OpenShockSetting.PrintAllText, OpenShockSetting.PrintMatchedText);

        RegisterParameter<int>(OpenShockParameter.Group, "VRCOSC/PiShock/Group", ParameterMode.Read, "Group", "Sets the specific group to use when using the non-specific action parameters");
        RegisterParameter<float>(OpenShockParameter.Duration, "VRCOSC/PiShock/Duration", ParameterMode.Read, "Duration", "The duration of the action as a 0-1 float for the group set by the Group parameter");
        RegisterParameter<float>(OpenShockParameter.Intensity, "VRCOSC/PiShock/Intensity", ParameterMode.Read, "Intensity", "The intensity of the action as a 0-1 float for the group set by the Group parameter");
        RegisterParameter<bool>(OpenShockParameter.Shock, "VRCOSC/PiShock/Shock", ParameterMode.Read, "Shock", "Shock the group set by the Group parameter");
        RegisterParameter<bool>(OpenShockParameter.Vibrate, "VRCOSC/PiShock/Vibrate", ParameterMode.Read, "Vibrate", "Vibrate the group set by the Group parameter");
        RegisterParameter<bool>(OpenShockParameter.Sound, "VRCOSC/PiShock/Beep", ParameterMode.Read, "Beep", "Beep the group set by the Group parameter");
        RegisterParameter<float>(OpenShockParameter.DurationGroup, "VRCOSC/PiShock/Duration/*", ParameterMode.Read, "Duration Group", "The duration of the action as a 0-1 float for a specific group\nE.G. VRCOSC/PiShock/Duration/0");
        RegisterParameter<float>(OpenShockParameter.IntensityGroup, "VRCOSC/PiShock/Intensity/*", ParameterMode.Read, "Intensity Group", "The intensity of the action as a 0-1 float for a specific group\nE.G. VRCOSC/PiShock/Intensity/0");
        RegisterParameter<bool>(OpenShockParameter.ShockGroup, "VRCOSC/PiShock/Shock/*", ParameterMode.Read, "Shock Group", "Shock a specific group\nE.G. VRCOSC/PiShock/Shock/0");
        RegisterParameter<bool>(OpenShockParameter.VibrateGroup, "VRCOSC/PiShock/Vibrate/*", ParameterMode.Read, "Vibrate Group", "Vibrate a specific group\nE.G. VRCOSC/PiShock/Vibrate/0");
        RegisterParameter<bool>(OpenShockParameter.SoundGroup, "VRCOSC/PiShock/Beep/*", ParameterMode.Read, "Beep Group", "Beep a specific group\nE.G. VRCOSC/PiShock/Beep/0");
        RegisterParameter<bool>(OpenShockParameter.Success, "VRCOSC/PiShock/Success", ParameterMode.Write, "Success", "Becomes true for 1 second if the action has succeeded");
    }

    protected override async Task<bool> OnModuleStart()
    {
        cancellationToken = new CancellationTokenSource();
        await reset();
        return await FetchShockers();
    }

    internal async Task<bool> FetchShockers()
    {
        var server = GetSettingValue<string>(OpenShockSetting.Server);
        var apiKey = GetSettingValue<string>(OpenShockSetting.APIKey);

        if (string.IsNullOrWhiteSpace(server))
        {
            Log("Please enter a server into the module settings");
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log("Please enter an API key into the module settings");
            return false;
        }

        openShockProvider = new OpenShockApiClient(new ApiClientOptions
        {
            Server = new Uri(server),
            Token = apiKey,
            Program = new ApiClientOptions.ProgramInfo
            {
                Name = "Code's VRCOSC OpenShock Module",
                Version = Assembly.GetExecutingAssembly().GetName().Version
            }
        });

        try
        {
            var shockers = await openShockProvider.GetOwnShockers(cancellationToken.Token);

            if (shockers.TryPickT0(out var shocker, out _))
            {
                var shockerList = shocker.Value.SelectMany(hub =>
                    hub.Shockers.Select(shockerItem => new ShockerWithHub
                    {
                        Hub = hub,
                        Shocker = shockerItem,
                    })
                ).ToList();
                ShockerWithHubs.Attribute.Clear();
                ShockerWithHubs.Attribute.AddRange(shockerList);
                var instancesNames = string.Join("', '", ShockerWithHubs.Attribute.Select(item => item.Shocker.Name));
                Log($"Shockers found: '{instancesNames}'");

                return true;
            }

            Log("API key is not valid? Could not get Own Shockers");
            return false;
        }
        catch (OpenShockApiError e)
        {
            Log($"OpenShockApiError whilst getting Own Shockers: {e.Message}");
            return false;
        }
    }

    protected override async Task OnModuleStop()
    {
        await cancellationToken.CancelAsync();
    }

    protected override async void OnAvatarChange(Avatar? avatar)
    {
        await reset();
    }

    private async Task reset()
    {
        var groups = GetSettingValue<List<ShockerGroup>>(OpenShockSetting.Groups);

        globalGroupID = null;
        globalDuration = 0f;
        globalIntensity = 0f;

        durations.Clear();
        intensities.Clear();

        foreach (var shockerGroup in groups)
        {
            durations[shockerGroup.ID] = 0f;
            intensities[shockerGroup.ID] = 0f;
        }

        try
        {
            var globalGroupParameter = await FindParameter("VRCOSC/PiShock/Group");
            globalGroupID = getShockerGroupFromIndex(globalGroupParameter?.GetValue<int>() ?? -1, out var group) ? group.ID : null;

            var globalDurationParameter = await FindParameter("VRCOSC/PiShock/Duration");
            globalDuration = globalDurationParameter?.GetValue<float>() ?? 0f;

            var globalIntensityParameter = await FindParameter("VRCOSC/PiShock/Intensity");
            globalIntensity = globalIntensityParameter?.GetValue<float>() ?? 0f;
        }
        catch
        {
        }

        for (var i = 0; i < groups.Count; i++)
        {
            try
            {
                var shockerGroup = groups[i];

                var durationParameter = await FindParameter($"VRCOSC/PiShock/Duration/{i}");
                durations[shockerGroup.ID] = durationParameter?.GetValue<float>() ?? 0f;

                var intensityParameter = await FindParameter($"VRCOSC/PiShock/Intensity/{i}");
                intensities[shockerGroup.ID] = intensityParameter?.GetValue<float>() ?? 0f;
            }
            catch
            {
            }
        }

        shock = null;
        vibrate = null;
        beep = null;
        shockExecuted = false;
        vibrateExecuted = false;
        beepExecuted = false;

        executedPhrases.Clear();
    }

    public void OnPartialSpeechResult(string text)
    {
        handleSpeechText(text);
    }

    public void OnFinalSpeechResult(string text)
    {
        handleSpeechText(text);
        if (GetSettingValue<bool>(OpenShockSetting.PrintAllText) || (GetSettingValue<bool>(OpenShockSetting.PrintMatchedText) && executedPhrases.Count > 0))
            Log($"Detected following text: '{text}'");
        executedPhrases.Clear();
    }

    private void handleSpeechText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        foreach (var phrase in GetSettingValue<List<Phrase>>(OpenShockSetting.Phrases).Where(phrase => !executedPhrases.Contains(phrase.ID) && phrase.Matches(text)))
        {
            var log = $"Found phrase '{phrase.Text.Value}'";
            Log(log);
            executedPhrases.Add(phrase.ID);
            Task.WaitAll(phrase.ShockerGroups
                               .DistinctBy(shockerGroupID => shockerGroupID.Value)
                               .Select(shockerGroupID => ExecuteGroupAsync(shockerGroupID.Value, phrase.Mode.Value, phrase.Intensity.Value, phrase.Duration.Value, log)),
                cancellationToken.Token
            );
        }
    }

    [ModuleUpdate(ModuleUpdateMode.Custom)]
    private async void checkForActions()
    {
        var delay = TimeSpan.FromMilliseconds(GetSettingValue<int>(OpenShockSetting.ButtonDelay));

        if (shock is not null)
        {
            if (shock.Value.Item1 + delay <= DateTimeOffset.Now && !shockExecuted)
            {
                shockExecuted = true;
                await HandleAction(shock.Value.Item2, ControlType.Shock);
            }
        }
        else
        {
            shockExecuted = false;
        }

        if (vibrate is not null)
        {
            if (vibrate.Value.Item1 + delay <= DateTimeOffset.Now && !vibrateExecuted)
            {
                vibrateExecuted = true;
                await HandleAction(vibrate.Value.Item2, ControlType.Vibrate);
            }
        }
        else
        {
            vibrateExecuted = false;
        }

        if (beep is not null)
        {
            if (beep.Value.Item1 + delay <= DateTimeOffset.Now && !beepExecuted)
            {
                beepExecuted = true;
                await HandleAction(beep.Value.Item2, ControlType.Sound);
            }
        }
        else
        {
            beepExecuted = false;
        }
    }

    public async Task HandleAction(Guid? groupId, ControlType mode)
    {
        float localIntensity;
        float localDuration;
        Guid? localGroup;

        if (groupId is not null && groupId == Guid.Empty)
        {
            localIntensity = intensities[(Guid)groupId];
            localDuration = durations[(Guid)groupId];
            localGroup = groupId;
        }
        else
        {
            localIntensity = globalIntensity;
            localDuration = globalDuration;
            localGroup = globalGroupID;
        }

        if (localGroup is null || localGroup == Guid.Empty)
        {
            Log("Cannot execute: Invalid group selected");
            return;
        }

        await ExecuteGroupAsyncFloat((Guid)localGroup, mode, localIntensity, localDuration);
    }

    public Task<bool> ExecuteGroupAsyncFloat(Guid groupId, ControlType mode, float intensityPercentage, float durationPercentage, string? customName = null, bool exclusive = false)
    {
        var shockerGroup = GetSettingValue<List<ShockerGroup>>(OpenShockSetting.Groups).SingleOrDefault(shockerGroup => shockerGroup.ID == groupId);
        if (shockerGroup is null) return Task.FromResult(false);

        var intensity = (byte)Math.Clamp((int)float.Round(shockerGroup.MaxIntensity.Value * intensityPercentage), 0, 100);
        var duration = (byte)Math.Clamp((int)float.Round(shockerGroup.MaxIntensity.Value * durationPercentage), 300, 30000);

        Log($"Executing {mode.ToLookup()} for group '{shockerGroup.Name.Value}' at {intensity}% for {MathF.Round(duration / 1000f, 1, MidpointRounding.AwayFromZero)}s");

        return ExecuteGroupAsync(groupId, mode, intensity, duration, customName, exclusive);
    }

    public async Task<bool> ExecuteGroupAsync(Guid groupId, ControlType mode, byte intensity, ushort duration, string? customName = null, bool exclusive = false)
    {
        var shockerGroup = GetSettingValue<List<ShockerGroup>>(OpenShockSetting.Groups)
            .SingleOrDefault(shockerGroup => shockerGroup.ID == groupId);

        if (shockerGroup is null)
        {
            Log($"Could not find group: {groupId}");
            return false;
        }

        var localIntensity = Math.Min(intensity, shockerGroup.MaxIntensity.Value);
        var localDuration = Math.Clamp(duration, (ushort)300, shockerGroup.MaxDuration.Value);

        Log($"Executing {mode.ToLookup()} for group '{shockerGroup.Name.Value}' at {localIntensity}% for {MathF.Round(localDuration / 1000f, 1, MidpointRounding.AwayFromZero)}s");

        var shockerIds = shockerGroup.Shockers.DistinctBy(shockerID => shockerID.Value)
                                     .Select(item => item.Value)
                                     .ToImmutableArray();
        var result = await executeShockersAsync(shockerIds, mode, localIntensity, localDuration, customName, exclusive);

        if (!result)
        {
            Log($"Group '{shockerGroup.Name.Value}' had some failures");
            return false;
        }

        Log($"Group '{shockerGroup.Name.Value}' has succeeded");

        sendSuccessParameter();
        return true;
    }

    private async Task<bool> executeShockersAsync(IEnumerable<Guid> shockerIds, ControlType mode, byte intensity, ushort duration, string? customName = null,
                                                  bool exclusive = false)
    {
        duration = Math.Clamp(duration, (ushort)300, (ushort)30000);
        intensity = Math.Clamp(intensity, (byte)0, (byte)100);
        var instances = shockerIds
                        .Select(item => new Control
                        {
                            Id = item,
                            Type = mode,
                            Duration = duration,
                            Intensity = intensity,
                            Exclusive = exclusive,
                        }).ToImmutableArray();

        if (instances.Length == 0)
        {
            Log("Skipping Http Call, because no shockers are selected");
            return true;
        }

        var instancesNames = string.Join("\n\t", instances
            .Select(item =>
            {
                var id = ShockerWithHubs.Attribute.SingleOrDefault(i => i.Shocker.Id == item.Id);
                return $"Id: {item.Id}, Name: '{id?.Shocker.Name ?? ""}', Type: {item.Type}, Intensity: {item.Intensity}, Duration: {item.Duration}, Exclusive: {item.Exclusive}";
            })
        );

        Log($"Executing shockers: \n\t{instancesNames}'");

        try
        {
            var result = await openShockProvider.ControlShocker(new ControlRequest
            {
                Shocks = instances,
                CustomName = customName,
            }).WaitAsync(cancellationToken.Token);

            if (result.TryPickT0(out _, out var result1))
            {
                Log($"Successully executed shockers: \n\t{instancesNames}");
                return true;
            }

            if (result1.TryPickT0(out var notFoundOrNoAccess, out var result2))
            {
                Log($"Could not find Shocker or the API-Key has no access: {notFoundOrNoAccess.Value.ToString()}");
                return false;
            }

            if (result2.TryPickT0(out var paused, out var result3))
            {
                Log($"Shocker is Paused: {paused.Value.ToString()}");
                return false;
            }

            if (result3.TryPickT0(out var noPermission, out _))
            {
                Log($"No Permission to use Shocker: {noPermission.Value.ToString()}");
                return false;
            }

            Log("Unauthenticated");
            return false;
        }
        catch (OpenShockApiError e)
        {
            Log($"OpenShockApiError whilst getting Executing Shock: {e.Message}");
            return false;
        }
    }

    private async void sendSuccessParameter()
    {
        var wasAcknowledged = await SendParameterAndWait(OpenShockParameter.Success, true);
        if (wasAcknowledged) SendParameter(OpenShockParameter.Success, false);
    }

    private bool getShockerGroupFromIndex(int index, [NotNullWhen(true)] out ShockerGroup? shockerGroup)
    {
        var groups = GetSettingValue<List<ShockerGroup>>(OpenShockSetting.Groups);

        if (index < 0 || index >= groups.Count)
        {
            shockerGroup = null;
            return false;
        }

        shockerGroup = groups[index];
        return true;
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case OpenShockParameter.Group:
                var groupIndex = parameter.GetValue<int>();
                globalGroupID = getShockerGroupFromIndex(groupIndex, out var shockerGroup) ? shockerGroup.ID : null;
                break;

            case OpenShockParameter.Duration:
                globalDuration = float.Clamp(parameter.GetValue<float>(), 0f, 1f);
                break;

            case OpenShockParameter.Intensity:
                globalIntensity = float.Clamp(parameter.GetValue<float>(), 0f, 1f);
                break;

            case OpenShockParameter.Shock:
                shock = parameter.GetValue<bool>() ? (DateTimeOffset.Now, null) : null;
                break;

            case OpenShockParameter.Vibrate:
                vibrate = parameter.GetValue<bool>() ? (DateTimeOffset.Now, null) : null;
                break;

            case OpenShockParameter.Sound:
                beep = parameter.GetValue<bool>() ? (DateTimeOffset.Now, null) : null;
                break;

            case OpenShockParameter.DurationGroup:
                if (!parameter.IsWildcardType<int>(0))
                {
                    Log("Invalid group for specific group duration parameter");
                    return;
                }

                var groupIndex2 = parameter.GetWildcard<int>(0);

                if (getShockerGroupFromIndex(groupIndex2, out var shockerGroup2))
                    durations[shockerGroup2.ID] = float.Clamp(parameter.GetValue<float>(), 0f, 1f);

                break;

            case OpenShockParameter.IntensityGroup:
                if (!parameter.IsWildcardType<int>(0))
                {
                    Log("Invalid group for specific group intensity parameter");
                    return;
                }

                var groupIndex3 = parameter.GetWildcard<int>(0);

                if (getShockerGroupFromIndex(groupIndex3, out var shockerGroup3))
                    intensities[shockerGroup3.ID] = float.Clamp(parameter.GetValue<float>(), 0f, 1f);

                break;

            case OpenShockParameter.ShockGroup:
                if (!parameter.IsWildcardType<int>(0))
                {
                    Log("Invalid group for specific group shock parameter");
                    return;
                }

                var groupIndex4 = parameter.GetWildcard<int>(0);

                if (getShockerGroupFromIndex(groupIndex4, out var shockerGroup4))
                    shock = parameter.GetValue<bool>() ? (DateTimeOffset.Now, shockerGroup4.ID) : null;

                break;

            case OpenShockParameter.VibrateGroup:
                if (!parameter.IsWildcardType<int>(0))
                {
                    Log("Invalid group for specific group vibrate parameter");
                    return;
                }

                var groupIndex5 = parameter.GetWildcard<int>(0);

                if (getShockerGroupFromIndex(groupIndex5, out var shockerGroup5))
                    vibrate = parameter.GetValue<bool>() ? (DateTimeOffset.Now, shockerGroup5.ID) : null;

                break;

            case OpenShockParameter.SoundGroup:
                if (!parameter.IsWildcardType<int>(0))
                {
                    Log("Invalid group for specific group beep parameter");
                    return;
                }

                var groupIndex6 = parameter.GetWildcard<int>(0);

                if (getShockerGroupFromIndex(groupIndex6, out var shockerGroup6))
                    beep = parameter.GetValue<bool>() ? (DateTimeOffset.Now, shockerGroup6.ID) : null;

                break;
        }
    }

    private enum OpenShockSetting
    {
        Server,
        APIKey,
        ShockerCache,
        PrintAllText,
        PrintMatchedText,
        ButtonDelay,
        Groups,
        Phrases
    }

    private enum OpenShockParameter
    {
        Group,
        Duration,
        Intensity,
        Shock,
        Vibrate,
        Sound,
        DurationGroup,
        IntensityGroup,
        ShockGroup,
        VibrateGroup,
        SoundGroup,
        Success
    }
}