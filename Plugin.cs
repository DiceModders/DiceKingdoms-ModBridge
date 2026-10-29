using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.UiKit;

namespace DiceModders.ModBridge;

/// <summary>
/// Shows ModCore's data in UiKit's screens. UiKit knows nothing about the game's lobby or civilizations and ModCore knows nothing about screens;
/// this plugin is the only place that knows both. It listens to ModCore's events and pushes the new values into UiKit's handles.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("DiceModders.UiKit")]
[BepInDependency("DiceModders.ModCore")]
public class Plugin : BasePlugin {
	internal static new ManualLogSource Log;

	public override void Load() {
		Log = base.Log;
		var scope = UiKit.UiKit.CreateScope("DiceModders.ModBridge");
		if (scope == null) {
			Log.LogError("UiKit is not available; the lobby rules and civilization entries will not be shown");
			return;
		}
		scope.WhenReady(s => {
			LobbyRulesScreen.Install(s);
			CivilizationEntries.Install(s);
		});
	}
}