# Rigfall design

How the game works, with the numbers. Everything here comes from the JSON files in `Assets/Rigfall/Resources/Data` and the rules in `Assets/Rigfall/Scripts/Core`; tables were generated from the data.

## The loop: a finite state machine

`Rigfall.Core.StateMachine<GamePhase>` runs the game. Only these transitions are allowed (anything else is refused, so a bug can't jump from Day straight to Game Over):

```
Boot -> MainMenu -> Day -> Night -> Dawn -> Day -> Night -> ...   (endless)
                             \-> GameOver -> Day (retry from last dawn / restart)
MainMenu is reachable from anywhere. Day -> Day and Night -> Day are the pause-menu retries.
```

| State | What happens |
|---|---|
| **Day** (economy and prep) | No enemies. Move freely. Barrels were paid at dawn. Building slots are interactive: walk onto one to see the price and what it builds, hold E to buy. Tonight's lanes are previewed. Hold N to sound the night horn. |
| **Night** (combat) | Build prompts are hidden. The wave for this night spawns from the field's predefined spawn vectors. Enemy, tower and unit AI run. Every frame: `NightRules.Check(rig integrity, enemies alive, spawns pending)`: Main Rig at 0 → **Game Over**; nothing left → **Dawn**. |
| **Dawn** (reward) | Income, contract payout, repairs (knocked-out buildings rebuild for free), field records and stage unlocks, then one spin of the reward wheel. Continue → next Day, and a checkpoint is saved. |
| **Game Over** | The run's barrels and buildings are lost. Permanent progress is kept. Retry from the last dawn, restart from Day 1, change the loadout, or go to the menu. |

## Building on fixed slots

Each field has fixed `BuildingSlot`s (empty objects with a trigger collider). Walking in shows a prompt with the cost and what will be built. Buying instantiates the building's model and script immediately and hides the prompt, with a scale pop, particles, barrels flying from the hero and a sound.

Inheritance: `BuildingBase` → `MainRig`, `House` (pumpjacks and tank batteries), `Tower`, `Wall`, `Barracks`.

| Building | Role | Upgrade path |
|---|---|---|
| Main Rig | rig | Drilling Rig → Rig Floor Gunners → Top Drive Package → Super-Spec Rig |
| Pumpjack | house | Pumpjack → Long-Stroke Unit → Rod Pump Controller |
| Tank Battery | mill | Tank Battery → Vapor Recovery Unit / Fire Monitor → Tank Farm / Deluge Skid |
| Derrick Tower | tower | Derrick Lookout → Crown Block Sniper / Flare Stack → Monkeyboard Overwatch / Thermal Oxidizer |
| Blast Wall | wall | Blast Wall → Reinforced Berm / Barbed Casing |
| Crew Camp | barracks | Roughnecks / Tong Hands / Chain Spinners / Wildcatters → Second Shift → Veteran Crew |
| Perforation Range | barracks | Wireline Snipers / Rivet Gunners / Kick Hunters / Flare Gunners → Extra Gun Crew → Premium Charges |
| Company Man's Office | barracks | Iron Roughneck / HSE Medic / Flare Drone / Swamp Buggy Rider → Second Contractor → Senior Contractors |

Tower and wall upgrades branch: you pick one of two options at a tier, and the later options follow from that choice. Most upgrades need a minimum Main Rig tier (Modular Rig-Up lifts that for towers).

## Pathfinding, commands and targeting

- **NavMesh.** Each field bakes a NavMesh at runtime with `NavMeshSurface` (Unity AI Navigation). Enemies and units are `NavMeshAgent`s. Buildings carry a carving `NavMeshObstacle`, so walls really block lanes: enemies path around or stop and break through.
- **Follow & Hold.** Hold the command button: every unit inside a `Physics.OverlapSphere` around the hero switches from Defend to **Follow** and walks to a formation slot just behind the hero. Release it: they switch to **Hold** at that spot. A quick tap sends units back to their posts.
- **Targeting.** Towers, units and enemies pick targets in a coroutine every 0.2 s instead of every frame. Enemies prefer the closest units and hero, then buildings, then the Main Rig. Iron Roughnecks taunt enemies near them.

## Juice

Every interaction has feedback: `transform.localScale` punches and pop-ins (`Tweener`), pooled particle bursts (`Fx`: sparks, oil, dust, fire, mud, ice, coins, shockwaves, the Blowout Brilliant shimmer), screen shake, hit-stop on heavy hits, hit flashes, floating damage numbers and a positional sound for everything.

## The endless night

`WaveDirector.Plan` builds each night's wave from the field, night number, difficulty, modifiers and the run's seed, so a night always plays out the same way (which is what makes "retry from last dawn" fair).

- **Budget** = (8 + (5.5·n + 0.12·n²) · scaling) · difficulty enemy count · mutators, where n = night − 1. Each enemy costs its threat value (elites 2.5×).
- **Enemy health** × difficulty × (1 + 0.06·n·scaling) × (1 + 0.1·weeks survived). Damage grows 3% per night. A little harder every night, without end.
- **New enemies**: two types on night 1, then one more every 3 nights (4, 7, 10, ...), in each field's own order.
- **Bosses** every 7th night (7, 14, 21, ...; every 5th with Accelerated Drilling Schedule). They rotate through the four bosses and gain 60% health each lap. Boss nights spend 40% less on the rest of the wave.
- **Lanes**: spawn vectors open by night number (`fromNight`); the main wave favours one lane.

| Field | Setting | Description | First enemies |
|---|---|---|---|
| Permian Pad | Onshore | A dusty multi-well pad in West Texas shale country. Wide open lanes and plenty of room for pumpjacks. The place to learn the job. | Gas Kick, Sand Crawler, then Flash Fire, Corrosion Beetle, H2S Wraith ... |
| Delta Swamp | Onshore | Wells on board-roads through mangrove swamp. Waxy crawlers come out of the cold flowlines and bats nest in the flare. | Gas Kick, Paraffin Crawler, then Sour Bat, Bitumen Ooze, H2S Wraith ... |
| Arctic Slope | Onshore | An ice pad on the North Slope. Hydrates form in every line, and the golems they make hit like trucks. | Gas Kick, Sand Crawler, then Paraffin Crawler, Shale Slinger, Hydrate Golem ... |
| North Sea Jacket | Offshore | A fixed steel jacket platform in a grey sea. Enemies climb up the risers onto three walkways. No room to waste. | Gas Kick, Riser Eel, then Sour Bat, Corrosion Beetle, H2S Wraith ... |
| Deepwater FPSO | Offshore | A floating production ship 200 km from shore, moored over 2,000 m of water. Four walkways, rusty decks and the hardest nights. | Gas Kick, Riser Eel, then Shale Slinger, Corrosion Beetle, H2S Wraith ... |

Each field unlocks the next when you clear its Stage I (survive night 7). Stages I, II and III (nights 7, 14, 21) also unlock that field's reward weapons.

| Enemy | Type | Description |
|---|---|---|
| Gas Kick | Ground | A bubble of formation gas that has found its way up the annulus. Small, fast and always first. |
| Sand Crawler | Ground | Produced sand that learned to walk. Slow, sturdy and abrasive. |
| Flash Fire | Ground | A running fireball that throws itself at the nearest building and explodes. |
| H2S Wraith | Flying | A drifting cloud of sour gas. Flies over walls and breathes poison. Only ranged defenders can reach it. |
| Corrosion Beetle | Ground | Armoured, slow and eats steel. Deals heavy damage to buildings. |
| Shale Slinger | Ranged | Snaps off flakes of fissile shale and throws them from behind the pack. |
| Hydrate Golem | Ground | Gas locked in ice, shaped like a man. Huge health, slow, crushing blows. |
| Bitumen Ooze | Ground | A blob of heavy oil. Splits into two droplets when it bursts. |
| Bitumen Droplet | Ground | What's left of an ooze. Still sticky. |
| Sour Bat | Flying | Fast flying pest that nests in flare stacks. Weak, but comes in flocks. |
| Scale Brute | Ground | Layer upon layer of barium sulfate scale. Very heavily armoured. |
| Riser Eel | Ground | Climbs up the risers from the seabed. Very fast melee striker. |
| Paraffin Crawler | Ground | Waxy crawler from cold flowlines. Its hits slow your hero and units. |
| The Blowout | Boss | An uncontrolled well given a body of fire. Stomps the ground and calls Flash Fires to its side. |
| Salt Dome Colossus | Boss | A diapir that rose too far. Every few seconds it slams the ground and stuns everything nearby. |
| Riser Kraken | Boss | Wrapped around the riser since the platform was installed. Sweeps tentacles and sends eels. |
| Hydrate Leviathan | Boss | A frozen beast of methane ice. Its breath freezes and slows everything in a long cone. |

## Difficulty

| Difficulty | Enemy HP | Enemy damage | Wave size | Enemy speed | Elite chance | Income | Start barrels | Growth per night | Hero respawn | Score |
|---|---|---|---|---|---|---|---|---|---|---|
| Easy | ×0.7 | ×0.7 | ×0.75 | ×0.95 | 0% | ×1.2 | +4 | ×0.8 | ×0.7 | ×0.6 |
| Normal | ×1.0 | ×1.0 | ×1.0 | ×1.0 | 3% | ×1.0 | +0 | ×1.0 | ×1.0 | ×1.0 |
| Hard | ×1.3 | ×1.25 | ×1.2 | ×1.05 | 8% | ×0.95 | +0 | ×1.15 | ×1.2 | ×1.5 |
| Extreme | ×1.7 | ×1.5 | ×1.45 | ×1.1 | 15% | ×0.9 | -2 | ×1.3 | ×1.4 | ×2.2 |
| Blowout (+ all 19 mutators) | ×1.7 | ×1.5 | ×1.45 | ×1.1 | 15% | ×0.9 | -2 | ×1.3 | ×1.4 | ×3.5 |

**Blowout** is Extreme with every mutator switched on; the score multiplier stacks with all 19 mutators' multipliers.

## Engineers (classes) and weapons

Each class has 5 weapons, one per rarity. Weapons unlock at the start, by clearing field stages, through achievements, or from the reward wheel. Weapon levels (up to 5, bought with core samples) are permanent.

| Class | Role | Ability | Weapons (Common → Blowout Brilliant) |
|---|---|---|---|
| Driller | Melee bruiser | **Spud In**: Dash forward with the bit spinning, hitting everything in your path for 200% weapon damage. | Tricone Bit, PDC Bit, Hybrid Bit, Impregnated Diamond Bit, Black Diamond Core Bit |
| Toolpusher | Tank and commander | **Rally Horn**: Units within 12 m heal 30% and deal 40% more damage for 6 seconds. | Pipe Wrench, Chain Tongs, Power Tongs, Drill Collar Maul, Kelly Bar of the First Gusher |
| Perforation Specialist | Ranged damage | **Shaped Charge Volley**: Fire 9 shaped charges in a wide cone, each for 120% weapon damage. | Wireline Pistol, Through-Tubing Perf Gun, Shaped Charge Carbine, TCP Cannon, Plasma Pulse Perforator |
| Mud Engineer | Crowd control | **Kill Mud**: Unleash a wave of kill-weight mud: enemies within 8 m take 40 damage, are pushed back and slowed by 50% for 4 seconds. | Mud Hose, Jet Gun, Barite Blaster, Kill Mud Cannon, Heavyweight Kill Pill |
| Geophysicist | Area damage | **Seismic Shot**: Detonate a shot point: 60 damage to enemies within 7 m and stun them for 1.5 seconds. | Thumper Staff, Vibroseis Rod, Airgun Array, Geophone Spread, Full-Waveform Inversion Scepter |

Attack styles: melee arc (drill bits), ground slam (pipe tools), straight skill shots (perforating guns), a short-range spray cone (mud guns) and lobbed area pulses (seismic sources).

## Crews (units)

The three unit buildings stand in for the three Thronefall unit buildings:

| Rigfall building | Plays the role of | Units (role in brackets) |
|---|---|---|
| Company Man's Office | Hero's Quarter | Iron Roughneck (Golem), HSE Medic (Support Mage), Flare Drone (Firewing), Swamp Buggy Rider (Lizard Rider) |
| Perforation Range | Archery Range | Wireline Sniper (Longbow Archers), Rivet Gunner (Crossbowmen), Kick Hunter (Hunters), Flare Gunner (Fire Archers) |
| Crew Camp | Barracks | Roughneck (Knights), Tong Hand (Spearmen), Chain Spinner (Flails), Wildcatter (Berserks) |

| Unit | Building | Role | Description |
|---|---|---|---|
| Iron Roughneck | Company Man's Office | Sturdy frontline | An automated pipe-handling robot. Huge integrity and armour; enemies nearby prefer to hit it. |
| HSE Medic | Company Man's Office | Healer and shield | Heals nearby units and the hero, and holds up a blast shield that absorbs a share of ranged damage aimed at allies. |
| Flare Drone | Company Man's Office | Anti-air and splash | A hovering flare-gun drone. Shoots air and ground targets with bursting flares. |
| Swamp Buggy Rider | Company Man's Office | Fast mobile striker | A roustabout on an amphibious buggy. Gets anywhere first and hits on the move. |
| Wireline Sniper | Perforation Range | Long range, fragile | Picks off targets from very far away. Goes down quickly if anything reaches it. |
| Rivet Gunner | Perforation Range | Armoured heavy shots | Wears a welding apron and fires pneumatic rivets. Hard-hitting and hard to kill. |
| Kick Hunter | Perforation Range | Monster hunter | Specialises in big game: deals 150% bonus damage to elites and bosses. |
| Flare Gunner | Perforation Range | Splash and burn | Lobs flares that burst and leave enemies burning. |
| Roughneck | Crew Camp | Versatile melee | Hard hat, steel toes, sledgehammer. Good at everything. |
| Tong Hand | Crew Camp | Crowd control | Long tong arms keep enemies at a distance. Hits knock back and slow. |
| Chain Spinner | Crew Camp | Area damage | Swings the spinning chain in a full circle, hitting everything around. |
| Wildcatter | Crew Camp | Sacrifice-driven berserker | Fights harder the more hurt they are (up to double damage). When they fall, nearby allies are healed by 40. |

## Rarity and the reward wheel

Clearing a night earns one spin of the core sample wheel. Segment sizes follow the rarity weights; the Wildcatter's Luck perk shifts weight toward rarer segments. Blowout Brilliant is drawn at least 7° wide so you can see it, but its odds stay 0.5%.

| Rarity | Wheel chance |
|---|---|
| Common | 55.0% |
| Rare | 28.0% |
| Epic | 12.0% |
| Legendary | 4.5% |
| Blowout Brilliant | 0.5% |

Wheel rewards range from barrels and core samples to free work orders, run-long blessings and permanent unlocks of wheel perks, mutators and weapons.

## Perks (40, take 4)

A new player has **12** perks to choose from. **15** more come from achievements and **13** from the reward wheel. Only 4 can be taken into a run.

The four perks from the brief, renamed for the oil patch: Explosive Walls → **Shaped-Charge Walls**, Resilient Residences → **Resilient Pumpjacks**, Timber Scaffolding → **Modular Rig-Up**, Castle-Up synergy → **Mast Raise**.

| Perk | Rarity | Area | Effect | How to get it |
|---|---|---|---|---|
| Enhanced Oil Recovery | Common | Economy | Every pumpjack lifts +1 barrel at dawn. | Available from the start |
| Hardbanding | Common | Defense | Blast walls have 50% more integrity. | Available from the start |
| Top Drive | Common | Hero | Your hero attacks 20% faster. | Available from the start |
| Mud Motor | Common | Hero | Your hero moves 15% faster. | Available from the start |
| Hazard Pay | Rare | Units | All your units deal 20% more damage. | Available from the start |
| Monkeyboard Marksmen | Common | Towers | Derrick towers reach 20% further. | Available from the start |
| Safety Stand-Down | Common | Units | Units have 25% more health. | Available from the start |
| Royalty Interest | Rare | Economy | At dawn, earn 10% interest on unspent barrels (up to +5). | Available from the start |
| Crew Change | Common | Units | Fallen units are replaced 50% faster during the night. | Available from the start |
| Rate of Penetration | Common | Hero | Your hero deals 20% more damage. | Available from the start |
| Modular Rig-Up | Rare | Towers | Derrick tower upgrades are no longer limited by the Main Rig tier, and towers cost 1 barrel less to build. Pairs well with Mast Raise for cheap max-level towers. | Available from the start |
| Resilient Pumpjacks | Rare | Economy | Pumpjacks have double integrity and still pay at dawn after they are knocked out and rebuilt. Pairs with Fortified Pumpjacks for a risk-free economy that also shoots back. | Available from the start |
| Blowout Preventer | Epic | Defense | Once per night, when the Main Rig drops below 50% integrity, the rams close: it takes no damage for 5 seconds. | Achievement: Well Control |
| Cathodic Protection | Rare | Defense | All your buildings slowly repair themselves during the night (2 integrity per second). | Achievement: Integrity Test |
| Shaped-Charge Walls | Rare | Defense | When a blast wall is breached it detonates, dealing 60 damage around it. Elite enemies usually shrug it off, so it's best against swarms. | Achievement: Wall Street |
| Fortified Pumpjacks | Epic | Economy | Pumpjacks get a nail gun that fires at nearby enemies for 8 damage. Turns your oil field into a second wall of towers. | Achievement: Pumpjack Field |
| Hot Tapping | Rare | Hero | Your hero's hits set enemies on fire (4 damage per second for 3 seconds). | Achievement: Hot Work Permit |
| Downhole Telemetry | Epic | Hero | Your hero has a 15% chance to land a critical hit for double damage. | Achievement: Precision Drilling |
| Hydraulic Jars | Rare | Hero | Your hero's hits knock enemies back 60% further and deal 10% more damage. | Achievement: Overbalanced |
| Double Crew | Epic | Units | Every unit building fields one extra unit. | Achievement: Full Crew |
| Rope Access Team | Rare | Units | Your units reach 25% further. | Achievement: Long Reach |
| Flare Igniters | Epic | Towers | Derrick towers deal 25% more damage and set targets on fire. | Achievement: Flare Up |
| Joint Venture | Rare | Economy | Crew camps, perforation ranges and the company man's office, and their upgrades, cost 20% less. | Achievement: Prime Contractor |
| Kick Detection | Rare | Utility | See where tonight's wave will come from during the day. Enemies are slowed by 40% for the first 6 seconds of each night. | Achievement: Kick Drill |
| Fishing Job | Common | Hero | When your hero falls, they are back on the rig floor 50% faster. | Achievement: Field Hand |
| Mast Raise | Rare | Economy | Main Rig upgrades cost 30% less, and each Rig tier adds +1 barrel at dawn. | Achievement: Barrel Baron |
| H2S Scrubbers | Rare | Defense | Your hero, units and buildings take 30% less damage from flying enemies. | Achievement: Gas Detector |
| Flare Gas Recovery | Rare | Economy | Every enemy kill has an 8% chance to drop a barrel. | Reward wheel |
| Gusher | Blowout Brilliant | Economy | All income at dawn is increased by 30%. | Reward wheel |
| Deluge System | Rare | Defense | Your buildings take 25% less damage. | Reward wheel |
| Swivel Joint | Common | Hero | Your class ability recharges 25% faster. | Reward wheel |
| Second Barrier | Epic | Defense | The Main Rig has 40% more integrity. | Reward wheel |
| Kelly Drive | Rare | Hero | Your hero deals 12% more damage and attacks 12% faster. | Reward wheel |
| Overbalanced Mud | Epic | Hero | Enemies hit by your hero are slowed by 25% for 2 seconds. | Reward wheel |
| Spot Price Spike | Rare | Economy | Your first purchase each day costs 2 fewer barrels. | Reward wheel |
| Leading Hand | Common | Units | Your Follow command reaches 60% further and your units move 15% faster. | Reward wheel |
| Crown Saver | Legendary | Towers | Derrick towers attack 40% faster. | Reward wheel |
| Black Gold Rush | Legendary | Economy | Tank batteries pay +2 barrels and pumpjacks +1 barrel at dawn. | Reward wheel |
| Relief Well | Legendary | Defense | Once per run, when the Main Rig would fall, it survives with 30% integrity instead. | Reward wheel |
| Wildcatter's Luck | Epic | Utility | The reward wheel favours rarer segments, and every night survived gives +2 core samples. | Reward wheel |

## Mutators (19)

Mutators make a run harder (one makes it easier) and multiply the score. The thirteen Thronefall gods each have a Rigfall counterpart with the same theme; six more are new.

| Thronefall god | Rigfall mutator |
|---|---|
| War | High-Pressure Zone |
| Turtle | Hardrock Formation |
| Tiger | Accelerated Drilling Schedule |
| Snake | Tight Hole |
| Falcon | Gas Expansion |
| Wasp | Sour Gas Swarm |
| Phoenix | Re-Pressurization |
| Elite | Abnormal Pressure |
| Growth | Infill Drilling |
| Range | Long-Reach Laterals |
| Destruction | Corrosive Fluids |
| Death | Zero-Tolerance HSE |
| Chaos | Uncontrolled Flow |
| *(new)* | Shut-In Order, Price Crash, Lost Circulation, Stuck Pipe, Crew Fatigue, Hurricane Season |

| Mutator | Rarity | Score | Effect | How to get it |
|---|---|---|---|---|
| Shut-In Order | Common | ×0.5 | Regulators cap the field: enemies have 25% less health and deal 25% less damage. Score is halved. | Available from the start |
| Hardrock Formation | Common | ×1.35 | Enemies have 50% more health. | Available from the start |
| High-Pressure Zone | Common | ×1.3 | Enemies deal 50% more damage. | Available from the start |
| Price Crash | Common | ×1.35 | All income is reduced by 30%. | Available from the start |
| Gas Expansion | Common | ×1.25 | Enemies move 30% faster. | Available from the start |
| Infill Drilling | Common | ×1.35 | Every wave is 35% larger. | Available from the start |
| Lost Circulation | Common | ×1.25 | Every building and upgrade costs 20% more. | Available from the start |
| Stuck Pipe | Common | ×1.15 | Your hero moves 15% slower. | Available from the start |
| Re-Pressurization | Epic | ×1.4 | Defeated enemies have a 20% chance to reform once with half health. | Achievement: Salt of the Earth |
| Zero-Tolerance HSE | Legendary | ×1.5 | If your hero falls at night, they don't return until dawn. | Achievement: HPHT Certified |
| Uncontrolled Flow | Legendary | ×1.4 | Each night, one random extra hazard applies for that night only. | Achievement: Uncontrolled |
| Sour Gas Swarm | Rare | ×1.3 | Flying enemies appear from night 2 and make up a larger share of every wave. | Reward wheel |
| Abnormal Pressure | Rare | ×1.35 | Elite enemies are much more common. | Reward wheel |
| Long-Reach Laterals | Rare | ×1.15 | Ranged enemies reach 40% further. | Reward wheel |
| Corrosive Fluids | Rare | ×1.35 | Enemies deal double damage to buildings. | Reward wheel |
| Tight Hole | Epic | ×1.2 | Thick night fog: you can only see enemies close to your buildings and hero. | Reward wheel |
| Crew Fatigue | Rare | ×1.25 | Units don't heal at dawn, and fallen units take twice as long to replace. | Reward wheel |
| Hurricane Season | Epic | ×1.3 | Gale-force winds: derrick towers lose 25% range and attack 15% slower. | Reward wheel |
| Accelerated Drilling Schedule | Legendary | ×1.6 | Bosses arrive every 5 nights instead of every 7, and enemies come from one extra direction. | Reward wheel |

## Progress: what you keep

| Kept forever (Profile) | Lost when the rig falls (this run) |
|---|---|
| Unlocked perks, mutators and weapons, weapon levels, core samples, achievements, field stages and records, settings | Barrels, buildings and units, the current night, blessings |

Even the run isn't really lost: a checkpoint (`RunSnapshot`) is saved at the start of every day, so the Game Over screen offers **Retry from last dawn** (same buildings and barrels as that morning) as well as **Restart from Day 1**. Save data is treated as untrusted input: unknown ids are dropped and numbers clamped (`Progress.Sanitize`).

## Achievements and contracts

31 achievements; 15 of them grant the achievement perks, others grant weapons, mutators or core samples. Each day also offers one contract (side quest) such as "defeat N enemies with your hero", "don't lose a blast wall tonight" or "finish N build actions today", paid in barrels and core samples at dawn.

## Options

- **Tutorial** (10 guided steps), **Field Manual & Extras** (enemies, crews, buildings, weapons, perks, mutators, a glossary of real oilfield terms, a jukebox, records, credits), **Achievements**.
- **Graphics**: Low (no shadows, 40% particles, half the props), Medium (hard shadows, 2× MSAA), High (soft shadows, 4× MSAA, all particles); frame-rate cap, screen shake, camera distance, interface size, damage numbers.
- **Audio**: master, music, effects, ambience and interface volumes; a music-track override; dynamic night music; 3D sound; mute in background. Nine synthesised music tracks and four ambience beds.
- **Fonts**: Saira Stencil One for titles (stencilled like equipment markings) and Chakra Petch for text.
