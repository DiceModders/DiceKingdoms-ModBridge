using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace DiceModders.ModBridge;

/// <summary>
/// Shows ModCore's data in UiKit's screens. UiKit knows nothing about the game's lobby or civilizations and ModCore knows nothing about screens;
/// this plugin is the only place that knows both.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("DiceModders.UiKit")]
[BepInDependency("DiceModders.ModCore")]
public class Plugin : BasePlugin {
	internal static new ManualLogSource Log;

	public override void Load() {
		Log = base.Log;
		LobbyRulesScreen.Install();
		CivilizationEntries.Install();
	}
}
