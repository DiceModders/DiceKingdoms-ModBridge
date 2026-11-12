using System.Collections.Generic;
using DiceModders.ModCore;
using DiceModders.UiKit;
using DiceModders.UiKit.Native;
using Core = DiceModders.ModCore.ModCore;   // the class and its namespace share a name

namespace DiceModders.ModBridge;

/// <summary>
/// "Mod keys" in Settings > Input, under the game's own keys: one row per <c>ModCore.Hotkeys</c> key, grouped per mod, with the game's own key box.
/// Clicking the box waits for the next key press (Escape cancels) and binds it; "Default" at the end puts every mod key back.
/// The block is made when the first key exists and rebuilt when one is added; a row follows its key when it changes anywhere else.
/// </summary>
internal static class HotkeysScreen {
	private static UiScope _scope;
	private static SettingsBlockHandle _block;
	private static readonly Dictionary<Hotkey, SettingsRowHandle> Rows = new();

	public static void Install(UiScope scope) {
		_scope = scope;
		if (Core.Hotkeys.All.Count > 0) Rebuild();
		Core.Hotkeys.Added += _ => Rebuild();
		Core.Hotkeys.Changed += h => { if (Rows.TryGetValue(h, out var row)) row.SetText(Caption(h)); _clashes?.SetText(ClashText()); };
	}

	private static void Rebuild() {
		_block?.Dispose();
		Rows.Clear();
		_block = _scope.Native.Settings.AddBlock("modbridge.hotkeys", SettingsPage.Input, "Mod keys", 1000);
		if (_block == null) return;
		string mod = null;
		foreach (var h in Core.Hotkeys.All) {
			if (h.ModId != mod) { mod = h.ModId; _block.AddHeader(h.ModName); }
			var hotkey = h;
			SettingsRowHandle row = null;
			row = _block.AddKey(h.Title, Caption(h), () => {
				row?.SetText("...");
				hotkey.Capture(_ => row?.SetText(Caption(hotkey)));
			});
			if (row != null) Rows[h] = row;
		}
		_block.AddButton("All mod keys", "Default", () => { foreach (var h in Core.Hotkeys.All) h.Reset(); });
		_clashes = _block.AddInfo(ClashText());
	}

	private static SettingsRowHandle _clashes;

	/// <summary>One warning line per mod key that shares its key with another mod key or a game key (sometimes on purpose).</summary>
	private static string ClashText() {
		var lines = new List<string>();
		foreach (var h in Core.Hotkeys.All) {
			var c = h.Clashes();
			if (c.Count > 0) lines.Add(h.Keys + " (" + h.Title + ") is also used by " + string.Join(", ", c));
		}
		return lines.Count == 0 ? "No mod key shares a key with another key." : string.Join("\n", lines);
	}

	private static string Caption(Hotkey h) => string.IsNullOrEmpty(h.Keys) ? "-" : h.Keys;
}
