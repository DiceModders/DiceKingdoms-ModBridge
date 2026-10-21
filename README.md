# ModBridge

Connects [ModCore](https://github.com/DiceModders/DiceKingdoms-ModCore) (game and networking logic) with [UiKit](https://github.com/DiceModders/UIKit_Another_Version)
(native interface) for Dice Kingdoms mods (BepInEx 6 IL2CPP).

The three repositories are three separate libraries:

| Library | Knows about | Does not know about |
|---|---|---|
| **UiKit** | Screens, windows, buttons, settings rows, assets | The lobby, rules, civilizations |
| **ModCore** | Steam lobby, lobby rules, compatibility, civilizations | Any screen |
| **ModBridge** (this) | Both of them | Nothing else; it has no API of its own |

ModBridge is the only place where "data from ModCore" meets "screens from UiKit". Install it and this happens, with nothing for a mod author to call:

- **Lobby rules** - every rule registered with `ModCore.Lobby.Rules` gets a row in the Settings box of the lobby screen (section "Rules", grouped per mod).
  The host edits the rows before the match, everybody else sees the host's value read-only. A rule's `visible` callback shows or hides its row, and a mod whose rules are
  all hidden shows no header. Under the rules the section lists `ModCore.Lobby.Compatibility.Warnings` ("<name> does not have <mod>, this rule may desync.").
- **Civilizations** - every civilization registered with `ModCore.Civilizations.Add` gets an entry in the civilization drop-down of the Rules screen.

Mods that use ModCore do **not** depend on ModBridge: without it their rules still work, they just have no rows to edit them. List it as an *optional* dependency in your
Thunderstore manifest (or required, if your mod is useless without the screens).

---

## Install (players)

Put these three files into `Dice-Kingdoms\BepInEx\plugins`:

```
DiceModders.UiKit.dll
DiceModders.ModCore.dll
DiceModders.ModBridge.dll
```

Mod managers install them automatically as dependencies of any mod that lists them.

## Make your first mod

Read **[docs/FIRST_MOD.md](docs/FIRST_MOD.md)**: a step-by-step tutorial that starts from an empty folder and ends with a mod that has a lobby rule the host can set,
a button in the top bar, a window and a settings block. It also explains how to find things in the game with the tools we use (dnSpy / ILSpy on the interop DLLs, Ghidra, `uikit-sprites.txt`).

Short version of how the libraries are used in a mod:

```csharp
using DiceModders.ModCore;
using DiceModders.UiKit;
using Core = DiceModders.ModCore.ModCore;   // class and namespace share a name: alias inside DiceModders.* namespaces
using Ui   = DiceModders.UiKit.UiKit;

[BepInPlugin("com.example.mymod", "My Mod", "1.0.0")]
[BepInDependency("DiceModders.UiKit")]
[BepInDependency("DiceModders.ModCore")]
public class MyModPlugin : BasePlugin {
    public override void Load() {
        var meteors = Core.Lobby.Rules.Toggle("mymod.meteors", "Meteors", true);   // appears in the lobby (ModBridge draws it)
        Ui.TopBar.Add("mymod.panel", "hammer", () => { /* open a window */ }, "My Mod");
    }
}
```

## How it works

| File | Job |
|---|---|
| `Plugin.cs` | Entry point. `[BepInDependency]` on UiKit and ModCore so both load first; calls the two installers. |
| `LobbyRulesScreen.cs` | Adds a "Rules" block (`SettingsTab.Lobby`, order 1000) with `UiKit.Settings.Add` once the first rule exists. Builds one toggle or stepper per rule from `ModCore.Lobby.Rules.All`; `Enabled = rule.Editable`, `Visible = rule.Visible`; shows the description and the live warnings. |
| `CivilizationEntries.cs` | Mirrors `ModCore.Civilizations.All / Added / Removed` into `UiKit.Rules.AddCivilizationEntry` (and removes the entry again on `Remove`). |

Both files are short on purpose: the logic lives in the libraries, ModBridge only wires them together. Everything is refreshed by UiKit four times a second, so the rows follow the lobby live
(host changes a rule, a member joins, the match starts and the rows turn read-only).

### Adding a new bridge

If a future feature of ModCore needs a screen (or a UiKit widget needs game data), add it **here**, not into the libraries, so UiKit and ModCore stay independent. Pattern:

1. A file with an `internal static class XyzScreen { public static void Install() { ... } }`.
2. Read the data from `Core.*`, draw it with `Ui.*`, subscribe to ModCore's events to rebuild.
3. Call `Install()` from `Plugin.Load()`.
4. Document it in this README.

## Build from source

Requirements: .NET SDK 6 or newer, the game installed, UiKit and ModCore built (this project references their release DLLs: `..\UiKit\bin\Release\net6.0\` and `..\ModCore\bin\Release\net6.0\`;
clone the three repos side by side, or change the `HintPath`s in `ModBridge.csproj`).

```powershell
$game = "<your Dice-Kingdoms folder>"   # the folder that contains BepInEx, e.g. ...\steamapps\common\Dice-Kingdoms (Steam: Properties > Installed Files > Browse)
.\scripts\link-interop.ps1 $game
dotnet build -c Release --no-incremental
Copy-Item bin\Release\net6.0\DiceModders.ModBridge.dll "$game\BepInEx\plugins" -Force
```

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Build: `Could not find DiceModders.UiKit` | Build UiKit and ModCore first (Release), or fix the `HintPath`s. |
| Lobby has no "Rules" block | No mod registered a rule yet (the block appears with the first rule), or UiKit / ModCore is missing from `plugins`. |
| Rule rows are greyed out | You are not the host, or the match is running. |
| A civilization is missing in the Rules drop-down | The mod did not call `ModCore.Civilizations.Add`, or it did so after the Rules screen was created and the screen was not reopened. |

Logs: `BepInEx\LogOutput.log`, lines tagged `[ModBridge]`, `[UiKit]` and `[ModCore]`.
