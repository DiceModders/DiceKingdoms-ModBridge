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
	}

	private static void Rebuild() {
		_block?.Dispose();
		_block = null;
		Updaters.Clear();

		_block = _scope.Native.Settings.AddBlock("modbridge.lobby.rules", SettingsPage.Lobby, "Rules", 1000);
		if (_block == null) return;

		var groups = Core.Lobby.Rules.All.GroupBy(r => r.ModId).ToList();
		foreach (var group in groups) {
			var rules = group.ToList();
			var header = _block.AddHeader(rules[0].ModName);
			if (header != null) Updaters.Add(() => header.SetVisible(rules.Any(r => r.Visible)));
			foreach (var rule in rules) AddRule(rule);
		}

		var warnings = _block.AddInfo(string.Empty);
		if (warnings != null) Updaters.Add(() => warnings.SetText(string.Join("\n", Core.Lobby.Compatibility.Warnings)));

		SubscribeToLobby();
		Refresh();
	}

	private static void AddRule(ILobbyRule rule) {
		SettingsRowHandle row;
		switch (rule) {
			case LobbyRule<bool> toggle:
				row = _block.AddToggle(toggle.Title, toggle.Live, v => {
					if (_refreshing) return;
					toggle.TrySet(v);
					Refresh();   // snap back when the change was refused
				});
				if (row != null) Updaters.Add(() => row.SetValue(toggle.Live));
				Subscribe(toggle);
				break;
			case LobbyRule<int> number:
				// the game's own slider; a rule with a step snaps to it
				row = _block.AddSlider(number.Title, number.Min, number.Max, number.Live, v => {
					if (_refreshing) return;
					number.TrySet(Math.Clamp((int)Math.Round(v / number.Step) * number.Step, number.Min, number.Max));
					Refresh();
				}, "0", true);
				if (row != null) Updaters.Add(() => row.SetValue((float)number.Live));
				Subscribe(number);
				break;
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

	private static void Subscribe(ILobbyRule rule) {
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
		Core.Lobby.MatchStarted += Refresh;
		Core.Lobby.MatchEnded += Refresh;
	}

	private static void Refresh() {
		if (_refreshing) return;
		_refreshing = true;
		try {
			foreach (var update in Updaters) {
				try { update(); }
				catch (Exception e) { Plugin.Log.LogWarning("could not update a lobby rule row: " + e.Message); }
			}
		}
		finally { _refreshing = false; }
	}
}