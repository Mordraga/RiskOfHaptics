# RiskOfHaptics
Risk of Rain 2 BepInEx mod that maps game events to haptic feedback via Lovense Connect Game Mode API.

## Playstyle priorities

Feedback now uses a configurable hierarchy on each device. The highest-priority active effect wins; intensity no longer adds every source together. Higher numbers mean higher priority. Equal priorities keep the current effect, with a stable ordering when neither effect is already playing.

Choose **Priorities → Playstyle Preset** in Risk of Options. The presets are starting points based on combat loops, not fixed survivor assignments. A different loadout can suit a different profile. See the [survivor-group guide](docs/playstyle-profiles.md) for the reasoning and exact priorities.

| Profile | Suggested survivors / builds | Feedback emphasis |
| --- | --- | --- |
| RapidFire | Commando, nailgun MUL-T, proc-heavy builds | Kill-chain momentum; low health can interrupt |
| MobileKiting | Huntress, evasive ranged builds | Damage, low health, and threats getting too close |
| PrecisionBurst | Railgunner, precision Bandit, rebar MUL-T | Boss engagement and nearby threats over sustained kill chains |
| AbilityBurst | Artificer, caster-oriented Void Fiend | Boss engagement and kill bursts; less crowd noise |
| MeleeMomentum | Mercenary, Loader, saw MUL-T | Kill chains and actual damage; close crowds are expected |
| ZoneControl | Engineer, tactical Captain | Kill chains and teleporter progress over damage |
| Attrition | Acrid, damage-over-time builds | Survival during delayed kills; chains when threats ease |
| HealthTrading | REX, health-spending Void Fiend | Combat momentum over routine damage and low-health interruptions |

Balanced provides a general-purpose hierarchy. Every profile's priorities are editable from 0–100. Editing a rank changes the profile label to Custom without replacing the other ranks. **Save Custom Priorities** stores the current ranks; selecting another preset and then Custom recalls them. Current edits persist in the BepInEx config across restarts even without saving the Custom slot.

Playstyle presets change priorities only. Chill / Normal / Masochist / Custom still control intensity and timing independently. A priority of zero is the lowest rank, not a disable switch; use the feature checkbox or a zero effect multiplier to disable an effect.

## Patterns, routing, and controls

- Damage produces a sharp, decaying impact. Item tiers use one, two, or three pulses; boss engagement builds upward; victory plays an escalating sequence and a fade. Kill-chain expiry retains its taper.
- Each device chooses independently. An event routed to Toy 2 does not suppress Toy 1's continuous effects. Missing Toy 2 falls back to Toy 1. Changing routing clears effects and stops the previous outputs.
- Higher-priority effects interrupt lower-priority events. Suppressed/interrupted one-shots are discarded, never queued. Continuous effects resume if their conditions still apply. Continuous signals that round to zero yield to other effects. Silence inside a heartbeat or event pattern retains its priority, so lower effects cannot fill the gaps.
- Every effect passes through its effect multiplier, global multiplier, difficulty scaling, and **Controls → Maximum Intensity Percent**. The maximum rounds down to a supported 5% step (42% permits at most 40%). Zero global strength or a zero maximum mutes all output.
- **F8** or **Controls → Pause or Resume** toggles feedback. Escape only navigates game menus; opening settings, changing priorities, and returning to play do not pause feedback. An explicit feedback pause remains active until you resume it. Disconnect waits for the final stop attempt before clearing the device connection. Connecting discovers devices without automatically playing a full-strength test.
- **Controls → Preview Only** stops hardware output and displays two virtual devices. Select an effect under **Preview**, then press **Preview Effect** to audition it. Turn Preview Only off to resume hardware output. Previews honor priorities, feature toggles, intensity limits, and routing, so an active higher-priority effect can suppress a preview. Kill-chain preview auditions a taper on the continuous route; actual chain expiry uses the event route.
- The compact 180 × 34 overlay shows an intensity percentage and thin bar. Two connected devices appear side by side, in Toy 1 / Toy 2 order. A small caption identifies paused, preview, or offline state. Profile names and event diagnostics are not shown. Percentages are requested values, not hardware measurements. Enable the overlay to see silent previews.

Patterns are sampled locally and sent through the same short-lived vibration command path as continuous effects. Each device has at most one request in flight, with changed output sent at most 20 times per second and unchanged output refreshed every 1.5 seconds. The command expires after two seconds. Slow networking can soften or miss short pulses; verify the feel with your devices. Priority arbitration applies to this mod's effects; it does not coordinate priority with external apps or tips. Command format follows the [Lovense Standard API](https://developer.lovense.com/docs/standard-solutions/standard-api).

The existing kill hook counts player-team kills, including allies and turrets. It does not identify which survivor earned the kill. Each kill adds time to the current chain deadline, so rapid kills bank extra time. Skill-specific feedback, such as weak-point hits, reload timing, or corruption changes, is not implemented by these presets.

## Connection troubleshooting

Use the IP and **HTTPS port** displayed by Lovense's Game Mode screen, keep Game Mode enabled, and connect the game computer to the same reachable local network. The mod uses HTTPS; an HTTP port is not interchangeable with its HTTPS port.

Version 1.1.2 restores the original working connection implementation: a shared default `HttpClient`, the original `ServicePointManager` certificate callback, and JSON sent with `StringContent` / `PostAsync`. The per-handler certificate callback, custom `HttpWebRequest` replacement, and three-second timeout introduced during development have been removed. Connection errors still include the endpoint and full underlying exception in `BepInEx/LogOutput.log`.

After rebuilding, fully restart the game to load the new DLL. If connection still fails, the detailed exception distinguishes refused connections, timeouts, TLS errors, and rejected API requests.

## Build and validation

Requires a .NET SDK, .NET Framework 4.7.2 reference assemblies, the installed game's managed assemblies, BepInEx, Risk of Options, and the Newtonsoft.Json assembly bundled with RoR2BepInExPack. The defaults in `LovenseTest.csproj` point to this project's original local game/profile directories. Override `GameDir` and `BepInExDir` with MSBuild properties for another installation.

Build without installing into the game profile:

```sh
dotnet build LovenseTest.csproj -p:DeployPlugin=false
```

The existing default `dotnet build` behavior still copies the DLL into the configured BepInEx plugin folder. Set `DeployPlugin=false` for development checks. To keep generated files outside the checkout:

```sh
dotnet build LovenseTest.csproj -p:DeployPlugin=false -p:BaseIntermediateOutputPath=/tmp/riskofhaptics-build/obj/ -p:OutputPath=/tmp/riskofhaptics-build/bin/
```

Run the dependency-free engine and transport regression tests without the game or devices:

```sh
dotnet run --project tests/HapticTests.csproj
```

In-game checks still needed: audition each event in Preview Only; try simultaneous damage/kill chains under Balanced and ZoneControl; route continuous output to Toy 1 and events to Toy 2; verify caps, live rank edits, pause/resume, disconnect, and actual-device pulse timing. Confirm game hooks as both multiplayer host and client.
