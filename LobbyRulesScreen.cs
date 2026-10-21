using System;
using System.Collections.Generic;
using System.Linq;
using DiceModders.ModCore;
using DiceModders.UiKit;
using Core = DiceModders.ModCore.ModCore;   // the class and its namespace share a name
using Ui = DiceModders.UiKit.UiKit;

namespace DiceModders.ModBridge;

/// <summary>
/// The "Rules" block of the Settings > Lobby tab: one row per ModCore lobby rule, grouped per mod, editable for the host only, plus the "may desync" warning.
/// A row is shown or hidden by the rule's own <c>visible</c> callback; a mod whose rules are all hidden shows no header either.
/// </summary>
internal static class LobbyRulesScreen {
	private static bool _added;

	public static void Install() {
		// the block is added when the first rule exists (a block without rows would only show an empty "Rules" header)
		if (Core.Lobby.Rules.All.Count > 0) Add();
		Core.Lobby.Rules.Registered += _ => Add();
	}

	private static void Add() {
		if (_added) return;
		_added = true;
		Ui.Settings.Add(SettingsTab.Lobby, "Rules", Build, order: 1000);
	}

	private static void Build(SettingsSection s) {
		var groups = Core.Lobby.Rules.All.GroupBy(r => r.ModId).ToList();
		foreach (var group in groups) {
			var rules = group.ToList();
			s.Header(rules[0].ModName, new SettingsRowOptions { Visible = () => rules.Any(r => r.Visible) });
			foreach (var rule in rules) AddRule(s, rule);
		}
		s.Info(() => string.Join("\n", Core.Lobby.Compatibility.Warnings));
	}

	private static void AddRule(SettingsSection s, ILobbyRule rule) {
		var options = new SettingsRowOptions { Enabled = () => rule.Editable, Visible = () => rule.Visible };
		switch (rule) {
			case LobbyRule<bool> toggle:
				s.Toggle(toggle.Title, () => toggle.Live, v => toggle.TrySet(v), options);
				break;
			case LobbyRule<int> number:
				s.Stepper(number.Title, () => number.Live.ToString(),
					d => number.TrySet(Math.Clamp(number.Live + d * number.Step, number.Min, number.Max)), options);
				break;
			default:
				Plugin.Log.LogWarning($"lobby rule '{rule.Key}' has a type this plugin cannot show");
				return;
		}
		if (!string.IsNullOrWhiteSpace(rule.Description)) s.Info(rule.Description, new SettingsRowOptions { Visible = () => rule.Visible });
	}
}
