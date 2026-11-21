using System;
using System.Collections.Generic;
using System.Linq;
using DiceModders.ModCore;
using DiceModders.UiKit;
using DiceModders.UiKit.Native;
using Core = DiceModders.ModCore.ModCore;   // the class and its namespace share a name

namespace DiceModders.ModBridge;

/// <summary>
/// The "Rules" block of the Settings > Lobby tab: one row per ModCore lobby rule, grouped per mod, editable for the host only, plus the "may desync" warning.
/// A row is shown or hidden by the rule's own <c>Visible</c>; a mod whose rules are all hidden shows no header either.
/// Rules under a heading (a civilization's or a mod's own group) stay short: the heading's on/off switch (its first rule, when that is a tick) is a row
/// here, and its other rules too when there is only one; two or more open in the heading's own window (<see cref="GroupWindow"/>, like the game values).
/// UiKit rows keep the value they were given, so every ModCore event that can change a row ends in <see cref="Refresh"/>.
/// </summary>
internal static class LobbyRulesScreen {
	private static UiScope _scope;
	private static SettingsBlockHandle _block;
	private static readonly List<Action> Updaters = new();
	private static readonly HashSet<ILobbyRule> Subscribed = new();
	private static bool _refreshing;
	private static bool _subscribedToLobby;

	public static void Install(UiScope scope) {
		_scope = scope;
		// the block is added when the first rule exists (a block without rows would only show an empty "Rules" header)
		if (Core.Lobby.Rules.All.Count > 0) Rebuild();
		Core.Lobby.Rules.Registered += _ => Rebuild();
		Core.Lobby.Groups.Changed += () => { if (Core.Lobby.Rules.All.Count > 0) Rebuild(); };   // a rule moved under a heading
		Core.Texts.LanguageChanged += () => { if (_block != null) Rebuild(); };   // UiKit rows keep the text they were made with
	}

	private static void Rebuild() {
		_block?.Dispose();
		_block = null;
		Updaters.Clear();
		GroupWindow.Close();   // its rows were made with the old rules and texts

		_block = _scope.Native.Settings.AddBlock("modbridge.lobby.rules", SettingsPage.Lobby, Tx.T("rules.block"), 1000);
		if (_block == null) return;

		// rules without a heading: per mod, always open
		foreach (var group in Core.Lobby.Rules.All.Where(r => r.Group == null).GroupBy(r => r.ModId)) {
			var rules = group.ToList();
			var header = _block.AddHeader(rules[0].ModName);
			if (header != null) Updaters.Add(() => header.SetVisible(rules.Any(r => r.Visible)));
			foreach (var rule in rules) AddRule(rule, null);
		}

		// headings (one per civilization, or a mod's own): the on/off tick right here, the other rules here when there is one, else behind one button
		foreach (var heading in Core.Lobby.Groups.All) {
			var h = heading;
			var rules = h.Rules.Where(CanShow).ToList();
			if (rules.Count == 0) continue;
			var others = Others(rules);
			if (others.Count < rules.Count) AddRule(rules[0], h);   // the heading's switch
			if (others.Count == 1) AddRule(others[0], h);
			else if (others.Count > 1) {
				var row = _block.AddButton(Tx.T("rules.groupSettings", h.Title), Tx.T("rules.open"), () => GroupWindow.Toggle(h));
				if (row != null) Updaters.Add(() => row.SetVisible(others.Any(r => r.Visible)));
			}
		}

		var warnings = _block.AddInfo(string.Empty);
		if (warnings != null) Updaters.Add(() => warnings.SetText(string.Join("\n", Core.Lobby.Compatibility.Warnings)));

		SubscribeToLobby();
		Refresh();
	}

	/// <summary>The rules this screen has rows for (text rules carry data a mod's own window edits).</summary>
	internal static bool CanShow(ILobbyRule rule) => rule is LobbyRule<bool> || rule is LobbyRule<int>;

	/// <summary>A heading's rules without its on/off switch: the first rule, when it is a tick, is the heading's switch.</summary>
	internal static List<ILobbyRule> Others(List<ILobbyRule> rules) => rules.Count > 0 && rules[0] is LobbyRule<bool> ? rules.Skip(1).ToList() : rules;

	private static void AddRule(ILobbyRule rule, LobbyGroup heading) {
		SettingsRowHandle row;
		switch (rule) {
			case LobbyRule<bool> toggle:
				row = _block.AddToggle(Label(toggle, heading), toggle.Live, v => {
					if (_refreshing) return;
					toggle.TrySet(v);
					Refresh();   // snap back when the change was refused
				});
				if (row != null) Updaters.Add(() => row.SetValue(toggle.Live));
				Subscribe(toggle);
				break;
			case LobbyRule<int> choice when choice.Choices != null:
				// a few named values: the game's own drop-down
				row = _block.AddDropdown(Label(choice, heading), choice.Choices, choice.Live, i => {
					if (_refreshing) return;
					choice.TrySet(i);
					Refresh();
				});
				if (row != null) Updaters.Add(() => row.SetValue(choice.Live));
				Subscribe(choice);
				break;
			case LobbyRule<int> number:
				// the game's own slider; a rule with a step snaps to it
				row = _block.AddSlider(Label(number, heading), number.Min, number.Max, number.Live, v => {
					if (_refreshing) return;
					number.TrySet(Math.Clamp(number.Min + (int)Math.Round((v - number.Min) / number.Step) * number.Step, number.Min, number.Max));   // steps counted from Min (49, 51, 53 ... for a step of 2)
					Refresh();
				}, "0", true);
				if (row != null) Updaters.Add(() => row.SetValue((float)number.Live));
				Subscribe(number);
				break;
			case LobbyRule<string>:
				return;   // text rules carry data a mod's own window edits: no row here
			default:
				Plugin.Log.LogWarning($"lobby rule '{rule.Key}' has a type this plugin cannot show");
				return;
		}
		if (row != null) Updaters.Add(() => row.SetEnabled(rule.Editable).SetVisible(rule.Visible));

		if (!string.IsNullOrWhiteSpace(rule.Description)) {
			var info = _block.AddInfo(rule.Description);
			if (info != null) Updaters.Add(() => info.SetVisible(rule.Visible));
		}
	}

	/// <summary>
	/// A row under a heading says which heading it belongs to (no heading row above it), unless its title is the heading's; where the rules of several
	/// mods mix under one heading, it also says which mod it belongs to.
	/// </summary>
	internal static string Label(ILobbyRule rule, LobbyGroup heading) {
		if (heading == null) return rule.Title;
		string title = string.Equals(rule.Title, heading.Title, StringComparison.OrdinalIgnoreCase) ? rule.Title : heading.Title + ": " + rule.Title;
		return heading.Rules.All(r => r.ModId == rule.ModId) ? title : title + " (" + rule.ModName + ")";
	}

	/// <summary>A rule's title in its heading's window (the window's title names the heading).</summary>
	internal static string WindowLabel(ILobbyRule rule, LobbyGroup heading) =>
		heading.Rules.All(r => r.ModId == rule.ModId) ? rule.Title : rule.Title + " (" + rule.ModName + ")";

	internal static void Subscribe(ILobbyRule rule) {
		// each rule keeps one subscription for the life of the game, even when the block is rebuilt
		if (!Subscribed.Add(rule)) return;
		switch (rule) {
			case LobbyRule<bool> toggle: toggle.Changed += _ => Refresh(); break;
			case LobbyRule<int> number: number.Changed += _ => Refresh(); break;
		}
	}

	private static void SubscribeToLobby() {
		if (_subscribedToLobby) return;
		_subscribedToLobby = true;
		Core.Lobby.Changed += Refresh;
		Core.Match.Started += Refresh;
		Core.Match.Ended += Refresh;
	}

	internal static void Refresh() {
		if (_refreshing) return;
		_refreshing = true;
		try {
			foreach (var update in Updaters) {
				try { update(); }
				catch (Exception e) { Plugin.Log.LogWarning("could not update a lobby rule row: " + e.Message); }
			}
		}
		finally { _refreshing = false; }
		GroupWindow.Refresh();
	}
}