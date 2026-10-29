# Your first Dice Kingdoms mod

A tutorial from an empty folder to a working mod that uses all three libraries. Takes about half an hour.

## 0. What you need

- The game, with **BepInEx 6 (IL2CPP, "be" build)** installed and started **once** (this generates `BepInEx\interop`, the game's code as .NET assemblies).
- .NET SDK 6 or newer, and an editor (Visual Studio, Rider or VS Code).
- The three libraries in `BepInEx\plugins`: `DiceModders.UiKit.dll`, `DiceModders.ModCore.dll`, `DiceModders.ModBridge.dll`
  (build them from their repositories, or take a release).
- Optional but very useful: **dnSpy** or **ILSpy** (open `BepInEx\interop\Assembly-CSharp.dll` to read the game's classes) and Ghidra for deeper questions.

Which library for which job:

| I want to... | Library |
|---|---|
| Draw a window, a button in the top bar, rows in Settings, a column in the scoreboard | UiKit |
| Know about the lobby, add a host-controlled setting, add a civilization | ModCore |
| See those settings / civilizations on screen | ModBridge (players install it; you write no code) |
| Change the game's own behaviour | Harmony patches in your own mod (BepInEx ships HarmonyX) |

## 1. Create the project

```powershell
mkdir MyMod; cd MyMod
dotnet new classlib -n MyMod -f net6.0 -o .
```

Replace `MyMod.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>MyName.MyMod</AssemblyName>
    <Version>1.0.0</Version>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <LangVersion>latest</LangVersion>
    <RestoreAdditionalProjectSources>
      https://api.nuget.org/v3/index.json;
      https://nuget.bepinex.dev/v3/index.json;
      https://nuget.samboy.dev/v3/index.json
    </RestoreAdditionalProjectSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BepInEx.Unity.IL2CPP" Version="6.0.0-be.*" IncludeAssets="compile" />
  </ItemGroup>
  <ItemGroup>
    <!-- the game's interop assemblies: put a junction called "interop" next to the csproj (see step 2) -->
    <Reference Include="interop/*.dll"><Private>false</Private></Reference>
    <!-- the libraries: Private=false so they are NOT copied into your output -->
    <Reference Include="DiceModders.UiKit"><HintPath>C:\path\to\DiceModders.UiKit.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="DiceModders.ModCore"><HintPath>C:\path\to\DiceModders.ModCore.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
```

Delete the generated `Class1.cs`.

## 2. Link the game's interop folder

Any of the three repositories has `scripts\link-interop.ps1`. Copy it into your project and run it once:

```powershell
$game = "<your Dice-Kingdoms folder>"   # the folder that contains BepInEx, e.g. ...\steamapps\common\Dice-Kingdoms (Steam: Properties > Installed Files > Browse)
.\scripts\link-interop.ps1 $game
```

It creates the junction `interop` (add `interop/` to `.gitignore`). If the game updates, BepInEx regenerates the folder and the junction keeps pointing at it.

## 3. The plugin

`Plugin.cs`:

```csharp
using BepInEx;
using BepInEx.Unity.IL2CPP;
using DiceModders.ModCore;
using DiceModders.UiKit;
using Core = DiceModders.ModCore.ModCore;   // class and namespace share a name; the alias is needed only inside DiceModders.* namespaces
using Ui   = DiceModders.UiKit.UiKit;       // but is harmless everywhere else

namespace MyName.MyMod;

[BepInPlugin("myname.mymod", "My Mod", "1.0.0")]
[BepInDependency("DiceModders.UiKit")]
[BepInDependency("DiceModders.ModCore")]
public class Plugin : BasePlugin {
    public static LobbyRule<bool> Meteors;
    private UiScope _ui;
    private UiWindow _window;

    public override void Load() {
        // (a) A lobby rule: the host sets it in the lobby, everyone follows, frozen when the match starts.
        Meteors = Core.Lobby.Rules.Toggle("myname.meteors", "Meteors", true, "Disasters hit the islands.");

        // (b) One UiKit scope per mod. It owns everything you add and removes it when disposed.
        _ui = Ui.CreateScope("myname.mymod");

        // (c) Everything that touches the game's UI is registered from WhenReady (runs once, when the UI exists).
        _ui.WhenReady(ui => {
            // A button in the top bar (icon = name of a game sprite, see step 6).
            ui.Native.TopBar.AddIconButton("hammer", "My Mod", () => TogglePanel(ui));

            // A tab in Settings.
            ui.Native.Settings.AddTab("myname.mymod", "My Mod", tab => {
                tab.AddToggle("Show hints", true, v => ShowHints = v);
                tab.AddInfo("Changes apply immediately.");
            });
        });

        Core.Match.Started += () => Log.LogInfo($"match started, meteors={Meteors.Value}");
    }

    public static bool ShowHints = true;

    private void TogglePanel(UiScope ui) {
        if (_window != null) { _window.Close(); _window = null; return; }
        _window = ui.Custom.Window(w => {
            w.Size(320, 160).Draggable().Closable().Title("My Mod");
            w.Body.Paragraph($"Meteors: {Meteors.Value}");
        });
    }
}
```

Rules of thumb:

- Create exactly one scope in `Load()` and register UI from `scope.WhenReady`. Native additions (top bar, settings, scoreboard, rules) attach themselves when their screen appears.
- **Windows** (`ui.Custom.Window`) need the game's UI to exist. Create them from a click handler or `WhenReady`, not directly from `Load()`.
- Dispose the scope (`_ui.Dispose()`) if your mod can be unloaded; it removes everything it owns.
- Prefix every id/key with your name (`myname.`). Ids are shared by all mods.
- Read a rule's `.Value` where you use it; do not copy it into a field.

## 4. Build and install

```powershell
$game = "<your Dice-Kingdoms folder>"   # the folder that contains BepInEx, e.g. ...\steamapps\common\Dice-Kingdoms (Steam: Properties > Installed Files > Browse)
dotnet build -c Release
Copy-Item bin\Release\net6.0\MyName.MyMod.dll "$game\BepInEx\plugins" -Force
```

Start the game. Check `BepInEx\LogOutput.log` for `Loaded plugin My Mod`. You should see the hammer button in the top bar; in a lobby the host gets a "Rules > My Mod > Meteors" row
(ModBridge draws it, so it must be installed).

## 5. Change the game itself (Harmony)

UiKit and ModCore give you screens and lobby data; changing gameplay is done by patching the game. Find the method in dnSpy/ILSpy (`Assembly-CSharp.dll`), then:

```csharp
using HarmonyLib;

[HarmonyPatch(typeof(DiceKingdoms.Game.SomeClass), nameof(DiceKingdoms.Game.SomeClass.SomeMethod))]
internal static class MeteorPatch {
    static bool Prefix() => Plugin.Meteors.Value;      // return false = skip the original; only when the rule is on
}
// in Load():  Harmony.CreateAndPatchAll(typeof(Plugin).Assembly);
```

IL2CPP caveats: patch methods that exist in the interop assembly; use `TryCast<T>()` instead of C# casts between Il2Cpp types; delegates given to the game need
`DelegateSupport.ConvertDelegate<...>(...)` (UiKit already does this for everything it exposes). Prefer **postfix** patches; a prefix that returns `false` skips other mods' postfixes too.
Anything that changes *what happens in the match* must be a **lobby rule** (so every machine agrees), or every machine must run the same mod.

## 6. Finding things

| Question | Tool |
|---|---|
| What is the game class / method for X? | dnSpy / ILSpy on `BepInEx\interop\Assembly-CSharp.dll` (names are kept, bodies are stubs). |
| What does a method actually do? | Ghidra on `GameAssembly.dll` (search the function by its interop name) or the structure notes in the Modding Wiki. |
| What is the sprite name for an icon? | In code: `ui.Custom.Theme.SpriteNames("filter")` (see the UiKit README, "Theme"). |
| What does the game wiki say? | [DiceKingdoms-Modding-Wiki](https://github.com/DiceModders/DiceKingdoms-Modding-Wiki). |

## 7. Publishing

1. Build in Release; zip `MyName.MyMod.dll`, `manifest.json`, `icon.png` (256x256), `README.md`.
2. `manifest.json` dependencies: `BepInEx-BepInExPack_IL2CPP-...`, plus `DiceModders-UiKit-...` and `DiceModders-ModCore-...` (and `DiceModders-ModBridge-...` if you want the lobby rows). Use the exact names and versions listed on Thunderstore.
3. **Never** put the library DLLs into your package.
4. If your mod changes the simulation, say so in the README, and register those settings as lobby rules, so players get the desync warning instead of a broken match.

## Checklist before you release

- [ ] Ids and keys are prefixed with your name.
- [ ] No windows created in `Load()`.
- [ ] Simulation-changing options are lobby rules (`affectsSimulation: true`), cosmetic ones are not.
- [ ] Tested in a lobby with a second player who does **not** have the mod (you should see the warning, and the game should not crash).
- [ ] `BepInEx\LogOutput.log` has no errors from your mod.
