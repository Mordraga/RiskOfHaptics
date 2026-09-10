# Survivor groups and haptic profiles

These groups organize combat loops rather than ranged/melee labels alone. The mechanics below motivate the profiles; the priority values are our initial haptic design choices, not established optimal settings. Choose manually for the current build. Intensity, timing, and routing stay independent.

## How the groups differ

| Profile | Mechanical basis and suggested fits | Why these priorities |
| --- | --- | --- |
| RapidFire | Commando and nailgun MUL-T sustain repeated hits and on-hit effects. Their differing attack/proc characteristics are documented in the [proc table](https://riskofrain2.wiki.gg/wiki/Proc_Coefficient). | Kill chains lead routine damage so momentum remains recognizable. Low health interrupts. Ambient crowd/elite proximity has less priority. |
| MobileKiting | [Huntress](https://riskofrain2.wiki.gg/wiki/Huntress) attacks while moving and uses blinks to maintain spacing; other evasive ranged builds can fit. | Damage, low health, elite proximity, and crowd pressure precede chains. Losing safe space should be more noticeable than another routine kill. |
| PrecisionBurst | [Railgunner](https://riskofrain2.wiki.gg/wiki/Railgunner) uses weak points and reload timing. [Bandit](https://riskofrain2.wiki.gg/wiki/Bandit) rewards positioning and finishing shots. Rebar MUL-T is another candidate. | Boss engagement and nearby threats outrank generic kill volume. This is a threat-oriented approximation with existing events, not feedback for successful weak-point hits. Bandit built around repeated Lights Out resets may prefer RapidFire or MeleeMomentum. |
| AbilityBurst | [Artificer](https://riskofrain2.wiki.gg/wiki/Artificer) combines charged/recharging attacks and area damage, with Ion Surge providing vertical movement. Controlled-form [Void Fiend](https://riskofrain2.wiki.gg/wiki/Void_Fiend) can suit a caster-oriented profile. | Boss engagement leads, with low health and damage interrupting normal kill momentum. Chains rank above generic proximity effects. Unlike PrecisionBurst, nearby elites do not constantly take priority over the results of an ability burst. |
| MeleeMomentum | [Mercenary](https://riskofrain2.wiki.gg/wiki/Mercenary) uses invulnerability and cooldown interactions; [Loader](https://riskofrain2.wiki.gg/wiki/Loader) combines grappling, heavy impacts, and barrier gain. Saw MUL-T can also fit. | Nearby enemies are expected during normal attacks, so proximity/crowd feedback ranks low. Chains lead routine damage, while low health interrupts. Loader focused on isolated charged punches can choose PrecisionBurst instead. |
| ZoneControl | [Engineer](https://riskofrain2.wiki.gg/wiki/Engineer) places turrets and defensive tools. [Captain](https://riskofrain2.wiki.gg/wiki/Captain) uses tactical drops and beacons. | Chains and objective progress precede routine damage, reflecting successful setup and sustained engagements. Boss engagement still interrupts chains. Captain focused on offensive bursts may prefer AbilityBurst or RapidFire. |
| Attrition | [Acrid](https://riskofrain2.wiki.gg/wiki/Acrid) spreads damage over time; poison itself cannot deliver killing blows. Other damage-over-time builds can have a similar delay between engagement and kills. | Low health and damage lead kill chains, preserving immediate survival feedback while waiting for damage to finish. Chains remain ahead of ambient proximity once immediate danger subsides. It does not measure poison uptime. |
| HealthTrading | [REX](https://riskofrain2.wiki.gg/wiki/REX) spends health on some abilities and recovers it through other skills. [Void Fiend](https://riskofrain2.wiki.gg/wiki/Void_Fiend) can spend health through Corrupted Suppress. | Damage and low-health feedback rank below combat momentum, reducing interruption from expected health fluctuations. They still play when no higher effect is active. This lowers priority of all damage; it does not distinguish self-inflicted costs from enemy hits or switch profiles automatically with corruption. |

One survivor can fit multiple profiles. REX using alternatives without health costs may prefer Attrition or AbilityBurst; MUL-T's weapon selection can change its group. Profiles are open to all survivors, including modded or unlisted ones.

## Exact priorities

Higher wins on each device. Victory (100) and death (95) lead in every built-in profile. Both remain editable. Low health's priority applies throughout its heartbeat, including silent intervals.

| Profile | Damage | Chain | Low HP | Teleporter | Elite | Crowd | Item | Boss |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 80 | 60 | 75 | 40 | 50 | 45 | 30 | 90 |
| RapidFire | 80 | 85 | 90 | 30 | 50 | 55 | 40 | 75 |
| MobileKiting | 85 | 65 | 90 | 35 | 80 | 75 | 30 | 70 |
| PrecisionBurst | 80 | 55 | 88 | 40 | 85 | 75 | 45 | 90 |
| AbilityBurst | 80 | 75 | 85 | 40 | 55 | 50 | 30 | 90 |
| MeleeMomentum | 80 | 85 | 90 | 35 | 40 | 30 | 45 | 75 |
| ZoneControl | 60 | 85 | 80 | 70 | 55 | 50 | 65 | 90 |
| Attrition | 85 | 80 | 90 | 40 | 60 | 50 | 30 | 75 |
| HealthTrading | 35 | 85 | 55 | 60 | 70 | 65 | 40 | 90 |

With damage and a kill chain active together, Balanced selects damage; ZoneControl selects the chain. A boss event on another device affects only that device. A lower-ranked item event arriving during either effect is dropped, so it cannot play out of context later.

## Current signal limits

The mod observes received damage, player-team kills, health, teleporter charging, enemy proximity, item pickups, death, teleporter boss engagement, and victory. It does not observe shots fired, individual on-hit procs, skill cooldown cycles, weak-point hits, successful reloads, turret health, or corruption transitions. Profiles use these existing observations; adding those signals would be a separate feature.

Player-team kill counting also means another player's kills can drive a chain. This preserves the existing multiplayer behavior and includes Engineer turrets, but is not an individual-performance metric. Priorities do not change the current accumulated kill-chain deadline or kills-to-maximum setting.
