# ModBridge

Connects [ModCore](https://github.com/DiceModders/DiceKingdoms-ModCore) (game and networking logic) with [UiKit](https://github.com/DiceModders/UiKit)
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
- **Civilizations** - every civilization registered with `ModCore.Civilizations.Add` gets an entry in the civilization drop-down of the Rules screen (UiKit only draws the entry; ModCore owns the id, tech tree and sync).

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
    private UiScope _ui;

    public override void Load() {
        var meteors = Core.Lobby.Rules.Toggle("mymod.meteors", "Meteors", true);   // appears in the lobby (ModBridge draws it)
        _ui = Ui.CreateScope("com.example.mymod");
        _ui.WhenReady(ui => ui.Native.TopBar.AddIconButton("hammer", "My Mod", () => { /* open a window */ }));
    }
}
```

## How it works

| File | Job |
|---|---|
| `Plugin.cs` | Entry point. `[BepInDependency]` on UiKit and ModCore so both load first; creates the UiKit scope `DiceModders.ModBridge` and calls the two installers from `scope.WhenReady`. |
| `LobbyRulesScreen.cs` | Adds a "Rules" block (`SettingsPage.Lobby`, order 1000) with `scope.Native.Settings.AddBlock` once the first rule exists. Builds one toggle or slider per rule from `ModCore.Lobby.Rules.All`, grouped per mod; pushes `SetEnabled(rule.Editable)`, `SetVisible(rule.Visible)`, values and the live warnings through the row handles. |
| `CivilizationEntries.cs` | Mirrors `ModCore.Civilizations.All / Added / Removed` into `scope.Native.Rules.AddCivilization(id, name)` (and disposes the handle on `Remove`). |

Both files are short on purpose: the logic lives in the libraries, ModBridge only wires them together. UiKit is event-driven: ModBridge subscribes to ModCore's events
(`Lobby.Changed`, `MatchStarted`, `MatchEnded`, `Rules.Registered`, per-rule changes, `Civilizations.Added/Removed`) and pushes the new state into the handles, so the rows follow the lobby live
(host changes a rule, a member joins, the match starts and the rows turn read-only). Nothing is polled.

### Adding a new bridge

If a future feature of ModCore needs a screen (or a UiKit widget needs game data), add it **here**, not into the libraries, so UiKit and ModCore stay independent. Pattern:

1. A file with an `internal static class XyzScreen { public static void Install(UiScope scope) { ... } }`.
2. Read the data from `Core.*`, draw it with the `UiScope` handles, subscribe to ModCore's events to update them.
3. Call `Install(scope)` from the `WhenReady` callback in `Plugin.Load()`.
4. Document it in this README.

## Build from source

Requirements: .NET SDK 6 or newer, the game installed, UiKit and ModCore built (this project references their release DLLs: `..\UiKit\bin\Release\net6.0\DiceModders.UiKit.dll` and `..\ModCore\bin\Release\net6.0\`;
clone the three repos side by side, or pass the path: `dotnet build -c Release -p:UiKitDll=C:\path\to\DiceModders.UiKit.dll`).

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
| A civilization is missing in the Rules drop-down | The mod did not call `ModCore.Civilizations.Add`, or UiKit has not yet seen the Rules screen (the entry attaches itself when the screen appears). |

Logs: `BepInEx\LogOutput.log`, lines tagged `[ModBridge]`, `[UiKit]` and `[ModCore]`.


## License

SPDX-License-Identifier: LGPL-2.1-or-later. See [LICENSE](LICENSE).
