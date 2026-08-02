<p align="center"><img src="https://i.ibb.co/KjwfjqhF/banner-valheimqol.jpg"></p>
<a href="https://dathost.net/r/marlthon/valheim-server-hosting" target="_blank"><img src="https://i.ibb.co/gQvx05M/dathostlogothunder.png" align="right" alt="Marlthon"></a>

<img src="https://img.shields.io/badge/ValheimQoL-8A2BE2"> ![Version Badge](https://img.shields.io/badge/Version-0.0.14-6aa84f.svg) <img src="https://img.shields.io/badge/Created by: Marlthon-1d629f">

## ValheimQoL Description

<b>ValheimQoL</b> is a comprehensive collection of configurable quality-of-life improvements for Valheim.</br>
It improves farming, crafting, building, storage, fire sources, production machines, tamed creatures, multiplayer administration, HUD information, and many other everyday systems without adding new items or replacing Valheim's core progression.</br>

This mod was created around the quality-of-life improvements I personally enjoy most when playing Valheim. For me, these are fundamental improvements that make the gameplay experience better and less repetitive.</br>
Some features are disabled by default, so I strongly recommend reading the entire mod configuration file to discover everything that is available.</br>
I will also provide my personal configuration file for anyone who wants to use the same settings that I use:</br>

<b><a href="https://raw.githubusercontent.com/Marlthon/ValheimQoL/master/marlthon.ValheimQoL.cfg">Download my personal ValheimQoL configuration</a></b>

## Main Features

<details>
<summary><b>FARMING AND HARVESTING</b> (<i>click to expand</i>)</summary>

### Grid Planting

The cultivator can plant crops in an organized rectangular grid instead of placing every seed manually.</br>

- Configurable rows and columns. The default grid is 2x2.
- Supports up to 25 plants in one action.
- Automatically respects the plant's required growing distance.
- Configurable minimum spacing between grid positions.
- The grid begins from an organized corner and fills positions in order.
- If the player does not have enough seeds for the full grid, only the available number is planted.
- Each successfully created plant consumes its own seed or planting resource.
- A placement preview shows every planned position before planting.
- Valid and invalid positions remain visible in the preview.
- The build HUD can display the total cost of the grid.
- The first plant uses Valheim's normal stamina cost.
- Extra plants can consume a separately configurable amount of stamina, or their extra stamina cost can be disabled.
- Seeds may also be taken from nearby accessible containers when <b>Craft From Containers</b> is enabled.

### Bulk Harvesting

Hold <b>Shift + Use</b> while interacting with a ready plant to harvest nearby crops in one action.</br>

- Default harvest radius: 3 meters.
- Default maximum: 50 targets per action.
- Can harvest only the same plant type or every eligible plant in range.
- Can include nearby beehives that contain honey.
- Optional hover-text hint explains the shortcut in game.
- The original target and every additional target are processed through their normal game interaction.

</details>

<details>
<summary><b>CRAFTING, BUILDING AND NEARBY CONTAINERS</b> (<i>click to expand</i>)</summary>

### Craft From Containers

Crafting, building, and cultivator planting can use materials stored in nearby player-built containers.</br>

- Default container search range: 20 meters.
- Crafting recipes can use nearby resources.
- Building pieces can use nearby resources.
- Complete planting grids can use seeds from nearby containers.
- Optional <b>LeaveOneItem</b> setting keeps one matching item in each container.
- Private containers and ward-protected containers are respected by default.
- Resources are removed only when the action is accepted.
- Designed for multiplayer use with server-controlled gameplay settings.

### Workbenches and Repair

- Configurable crafting-station building radius. Default: 20 meters.
- Configurable player-base radius used by systems such as enemy spawn suppression. Default: 20 meters.
- Configurable extra radius per valid crafting-station upgrade level. Default: 5 meters.
- Optional removal of the roof requirement for crafting stations. Disabled by default.
- Interacting with a valid repair station repairs every repairable equipped and inventory item instead of repairing only one item at a time.
- Hammer repair can repair all damaged structures inside a configurable area. Default radius: 10 meters.

### Building Convenience

- Configurable building placement distance.
- Building inside dungeons is enabled by default.
- Nearby comfort-piece detection is increased from 10 to 20 meters.
- Heat effect range is increased to 20 meters.

</details>

<details>
<summary><b>STORAGE, ITEMS AND INVENTORY</b> (<i>click to expand</i>)</summary>

### Configurable Vanilla Containers

The number of rows and columns can be configured for supported player-built vanilla containers.</br>

| Container | Default Size |
|---|---:|
| Personal Chest | 2 x 3 |
| Wooden Chest | 2 x 5 |
| Reinforced Chest | 4 x 6 |
| Black Metal Chest | 4 x 8 |
| Barrel | 3 x 3 |

Existing supported chests are updated when their configuration changes.</br>

### Item Balance

- Configurable base player carry capacity. Default: 300.
- Configurable Megingjord carry bonus. Default: 150.
- Configurable global non-coin item weight multiplier. Default: 1, which preserves vanilla weight.
- Configurable global stack-size multiplier. Default: 1, which preserves vanilla stack sizes.
- Coins have a configurable weight. Default: 0.01 per coin.
- Coin stacks can contain up to 9,999 coins by default.

### Floating Items

Eligible dropped items float on water instead of sinking.</br>
Heavy or progression-sensitive materials can be excluded through a comma-separated prefab list. The default exclusion list includes ores, metal, black metal scrap, crystal, chitin, tar, stone blocks, dragon eggs, and other heavy resources.</br>

### Delete Items

Pick up an item with the inventory cursor and press the configured delete key to permanently remove it.</br>

- Default key: <b>Delete</b>.
- Equipped items cannot be deleted.
- This key setting is local and is not controlled by the server.

</details>

<details>
<summary><b>FIRES, COOKING AND PRODUCTION MACHINES</b> (<i>click to expand</i>)</summary>

### Fire Sources

- Campfires, hearths, torches, sconces, braziers, and other Fireplace-based objects can work without consuming fuel.
- Fireplace-based objects can be turned on and off with the normal Use key.
- Fires may still be extinguished by rain. When an infinite, toggleable fire was automatically extinguished by rain, it relights after the rain ends.
- Smoke creation and smoke blockage can optionally be disabled while keeping fire and heat active. This option is disabled by default.
- Furnaces and blast furnaces are not altered by the fireplace smoke-removal logic.

### Stone Oven and Hot Tub

- The Stone Oven can operate without consuming Wood. Food requirements and cooking duration remain unchanged.
- The Hot Tub can remain heated without consuming Wood.
- When Hot Tub infinite fuel is enabled, its Wood interaction becomes a persistent <b>On/Off</b> switch.
- The Hot Tub state is saved in the world and synchronized in multiplayer.
- Turning the Hot Tub on controls both its effects and its gameplay functionality.

### Bulk Machine Feeding

Hold <b>Shift + Use</b> to add as many valid items as possible to a supported production machine in one action.</br>

Supported machines include:

- Smelters
- Blast furnaces
- Charcoal kilns
- Windmills
- Spinning wheels
- Eitr refineries

The machine's remaining capacity and the player's available items are always respected.</br>

### Expanded Machine Capacity

| Machine | Capacity Applied by ValheimQoL |
|---|---:|
| Smelter | 100 ore / 200 fuel |
| Blast Furnace | 100 ore / 200 fuel |
| Eitr Refinery | 100 material / 200 fuel |
| Charcoal Kiln | 200 wood |
| Windmill | 100 barley |
| Spinning Wheel | 100 flax |
| Hot Tub | 50 fuel |

### Windmill Production

Windmill production can run at full power without depending on wind strength or wind cover.</br>
Only production is independent of the wind: the blades and sound continue to follow the real vanilla wind, preventing the windmill from constantly spinning and making maximum noise when the air is calm.</br>

</details>

<details>
<summary><b>HUD, CLOCK AND REMAINING-TIME INFORMATION</b> (<i>click to expand</i>)</summary>

### Clock

- Shows the current Valheim day and in-game time on the HUD.
- Can also show the computer's local time below the in-game clock.
- Configurable update interval.
- Configurable font sizes.
- Supports 24-hour or 12-hour real-time display.

### Hover Timers

Looking at supported objects can display a simple English <b>Remaining</b> timer.</br>

- Plant growth
- Fermenter processing
- Cooking Station food
- Stone Oven food and pies
- Beehive honey production
- Harvested pickable-resource respawn
- Creature taming
- Offspring and egg growth

When progress cannot continue, the timer can display <b>Paused</b>.</br>
Every hover timer has its own local configuration toggle and is enabled by default.</br>

</details>

<details>
<summary><b>DAY AND NIGHT</b> (<i>click to expand</i>)</summary>

ValheimQoL can independently configure how many real minutes daytime and nighttime last.</br>

- The custom day/night system is <b>disabled by default</b>, preserving Valheim's original time mapping.
- Default reference values are 21 real minutes of daytime and 9 real minutes of nighttime, matching Valheim's 30-minute vanilla cycle.
- Day and night duration values are independent, so their sum becomes the total cycle duration.
- Example: 30 minutes of day plus 10 minutes of night creates a 40-minute cycle.
- Sleeping and normal Valheim time skips remain supported.
- The authoritative network time remains shared in multiplayer and on dedicated servers.

</details>

<details>
<summary><b>BUILDING DURABILITY AND STRUCTURAL INTEGRITY</b> (<i>click to expand</i>)</summary>

### Weather Protection

- Prevents normal rain weathering damage to building pieces by default.
- Prevents underwater weathering damage by default.
- These options do not disable Ashlands ash or lava damage.

### Structural Integrity

Support loss over distance can be adjusted separately for:

- Wood
- Stone
- Iron
- Core Wood
- Black Marble
- Grausten
- Ancient material pieces

Each material accepts a value from 0% to 100%.</br>

- <b>0%</b> preserves vanilla structural support loss.
- <b>50%</b> halves support loss over distance.
- <b>100%</b> removes distance-based support loss and prevents unsupported pieces of that material from collapsing.

All per-material values default to 0%, so unlimited structural support is not enabled unless the server owner chooses it.</br>

</details>

<details>
<summary><b>TAMED CREATURES AND BALLISTAS</b> (<i>click to expand</i>)</summary>

### Tamed Creatures

- Every tamed creature can be made commandable like a wolf.
- The normal Use key toggles follow and stay behavior.
- A following tame can teleport near its player after falling farther behind than the configured distance. Default: 64 meters.
- Mounted creatures are never moved by the follow-teleport system.
- Following tames can accompany their player into and out of dungeons.
- Taming progress, offspring growth, and egg growth can be shown through local hover timers.

### Ballistas

- Ballistas do not target players by default.
- Ballistas do not target tamed animals by default.
- The protection applies to normal and configured ballista target modes.

</details>

<details>
<summary><b>DOORS, VEHICLES, MAP AND PLAYER CONVENIENCE</b> (<i>click to expand</i>)</summary>

### Doors

Player-built doors close automatically only after every player has moved away.</br>

- Default clear distance: 5 meters.
- Default delay after the area becomes clear: 2 seconds.
- The door stays open while any player remains nearby.

### Carts and Ships

- Carts can be deconstructed with the hammer.
- Ships can be deconstructed with the hammer.
- Vanilla safety and inventory checks remain in use.
- A ship cannot be removed while a player is aboard.

### Map Sharing

- Prevents players from disabling public map-position sharing by default.
- Server administrators can be exempt from this restriction.

### Player Convenience

- Configurable automatic item-pickup radius. Default: 2 meters.
- Configurable interaction distance. Default: 5 meters.
- Weapons, shields, tools, and torches remain equipped while swimming instead of being automatically put away.

</details>

<details>
<summary><b>SERVER AND OPTIONAL INTEGRATION FEATURES</b> (<i>click to expand</i>)</summary>

### Multiplayer Server

- Configurable maximum player count for Steam and PlayFab backends.
- Default ValheimQoL limit: 40 players. Vanilla limit: 10 players.
- Gameplay configuration is synchronized through ServerSync.
- With <b>Force Server Config</b> enabled, the dedicated server or host configuration overrides synchronized client values.
- The mod is required on both the server and connecting clients.
- ServerSync is bundled into the ValheimQoL DLL; a separate ServerSync plugin file is not required.

### Quick Connect

An optional local main-menu button can connect directly to a configured server.</br>

- Disabled by default.
- Configurable button text, server address, port, and password.
- These entries are local and are not synchronized by the server.
- The configured password is stored in the local configuration file, so protect that file if a private password is used.

### TargetPortals Protection

When portals from the <b>TargetPortals</b> mod are present, ValheimQoL can protect them from:

- Unauthorized portal-mode changes
- Damage
- Hammer removal
- Nearby terrain editing

The owner and server administrators retain permission. Protection and search radii are configurable.</br>

</details>

## Controls

| Action | Default Control |
|---|---|
| Plant a complete grid | Place a cultivator plant normally |
| Harvest nearby ready plants or beehives | Hold Shift + Use |
| Bulk-feed a production machine | Hold Shift + Use |
| Turn a supported fire source on or off | Use |
| Command a supported tame to follow or stay | Use |
| Permanently delete an inventory item | Hold the item with the cursor and press Delete |
| Area repair | Use the hammer's normal repair action |

## Configuration

<details>
<summary><b>CONFIGURATION DETAILS</b> (<i>click to expand</i>)</summary>

The configuration file is generated after starting the game once with the mod installed:</br>

`BepInEx/config/marlthon.ValheimQoL.cfg`

Every entry contains an English explanation and an example.</br>

| Configuration Type | Behavior |
|---|---|
| `[Synced with Server]` | The server or host value is used for connected players when server locking is enabled. |
| `[Not Synced with Server]` | Personal client preference; the server does not replace it. |

Local settings include the Quick Connect menu, delete-item key, and individual hover timers.</br>
Gameplay-affecting settings are synchronized so every connected player uses the same rules.</br>

ValheimQoL watches its configuration file and reloads saved changes while the game or server is running. Most settings are read immediately; changes that affect already-created prefab data may still require reconnecting or restarting the game to be fully reapplied.</br>

### Important Features Disabled by Default

The following features are available in the configuration file but preserve vanilla behavior until you deliberately enable or change them:</br>

<b>Custom Day and Night Durations</b></br>
Allows daytime and nighttime to have independently configured real-time durations. It is disabled by default, so Valheim keeps its original 30-minute cycle: approximately 21 real minutes of daytime and 9 real minutes of nighttime.</br>
Enable `DayNight.Enabled` and set `DayDurationMinutes` and `NightDurationMinutes` to create the cycle you want. The two values are added together to determine the complete cycle duration.</br>
Example: 30 minutes of day plus 10 minutes of night creates a 40-minute cycle in which daytime lasts three times as long as nighttime.</br>

<b>Quick Connect Main-Menu Button</b></br>
Adds a custom button to Valheim's main menu that connects directly to a configured server address and port. The button text, address, port, and password can be configured locally.</br>
It is disabled by default because every player may want to connect to a different server. These settings are not synchronized by the server.</br>
The password is saved in the local `.cfg` file as readable text, so do not share that file if it contains a private password.</br>

<b>Removing Crafting-Station Roof Requirements</b></br>
Allows crafting stations that normally require shelter, such as the Workbench, to be used without a roof.</br>
It is disabled by default because enabling it changes an intentional vanilla building requirement. Set `Workbench.NoRoofRequirement=true` if you want exposed outdoor crafting stations to remain usable.</br>

<b>Disabling Fire-Source Smoke</b></br>
Stops Fireplace-based objects from creating smoke and prevents smoke blockage from extinguishing those fires. Campfires, hearths, torches, sconces, braziers, and similar fire sources keep their flames, light, and heat.</br>
It is disabled by default to preserve vanilla smoke and chimney mechanics. Furnaces and blast furnaces are excluded so their production logic and visual activation are not affected.</br>

<b>Global Item Weight and Stack Multipliers</b></br>
The global weight multiplier changes the weight of every non-coin item. For example, `0.5` makes eligible items weigh half as much, while `2` doubles their weight.</br>
The global stack multiplier changes the maximum stack size of stackable non-coin items. For example, `2` changes a vanilla stack of 50 into a stack of 100.</br>
Both multipliers default to `1`, which leaves vanilla item weights and stack sizes unchanged.</br>

<b>Structural-Integrity Bonuses</b></br>
Reduces how quickly building support is lost over distance. The bonus can be configured separately for Wood, Stone, Iron, Core Wood, Black Marble, Grausten, and Ancient materials.</br>
Every material defaults to `0%`, which preserves vanilla structural integrity. A value of `50%` halves support loss, while `100%` removes distance-based support loss and prevents unsupported pieces of that material from collapsing.</br>

Because the mod contains many independent options, server owners should review the entire configuration file before opening a world to players.</br>

</details>

## Installation

<details>
<summary><b>MANUAL INSTALL</b> (<i>click to expand</i>)</summary>

1. Install the latest compatible BepInEx 5 version following the BepInEx author's instructions.
2. Place `ValheimQoL.dll` inside the `BepInEx/plugins/` folder.
3. Start Valheim once to generate `marlthon.ValheimQoL.cfg`.
4. Review the complete configuration file before playing.
5. For multiplayer or a dedicated server, install the same ValheimQoL version on the server and every client.

ValheimQoL is built for <b>Valheim 0.221.12</b>, BepInEx, Harmony, multiplayer, and dedicated servers.</br>

</details>

### Support the Development

By supporting me on <b>Patreon</b>, you help ensure continued support, regular updates, and the creation of new mods.</br>
Your support is completely optional, but greatly appreciated.</br>

<p align="left">
  <a href="https://patreon.com/u76220721?utm_medium=unknown&utm_source=join_link&utm_campaign=creatorshare_creator&utm_content=copyLink">
    <img src="https://i.ibb.co/zTyg5d6d/Patreon-Button.png" />
  </a>
  &nbsp;&nbsp;
  <a href="https://www.paypal.com/donate/?hosted_button_id=NU6HKJ8ZD9NU8">
    <img src="https://i.ibb.co/84bSC13P/Paypal-Button.png" />
  </a>
  &nbsp;&nbsp;
  <a href="https://discord.gg/B9TFJBnvzk">
    <img src="https://i.ibb.co/pjwkZ35P/Discord-Button.png" />
  </a>
  &nbsp;&nbsp;
  <a href="https://marlthon.com/">
    <img src="https://i.ibb.co/6R6Crj4d/Marlthon-Button.png" />
  </a>
</p>

---

<p align="center"><a href="https://marlthon.com/custom.php"><img src="https://i.ibb.co/YTJ91SF/Banner-Custom-Mods.png" /></a></p>
