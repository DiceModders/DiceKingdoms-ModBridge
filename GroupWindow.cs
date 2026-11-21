using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceModders.ModCore;
using DiceModders.UiKit;
using DiceModders.UiKit.Custom;
using UnityEngine;

namespace DiceModders.ModBridge;

/// <summary>
/// A heading's rules in their own window (Settings > Lobby > Rules > "&lt;heading&gt;: settings" > Open), so the rules list keeps one row per heading
/// (its on/off tick stays in the list). Ticks, drop-downs, and numbers with - / + and "Default", each with its description. The host edits; everybody
/// else sees the host's values. Made once when opened; afterwards only values, who may edit and which rows show are updated (<see cref="Refresh"/>,
/// called from the rules list's own refresh), so no control is rebuilt from inside its own click.
/// </summary>
internal static class GroupWindow {
	private static readonly Color Changed = new(1f, 0.82f, 0.45f, 1f);
	private static readonly Color Normal = new(0.97f, 0.94f, 0.89f, 1f);
	private static readonly Color Dim = new(0.80f, 0.76f, 0.70f, 1f);

	private static UiScope _scope;
	private static UiWindow _win;
	private static string _key;
	private static readonly List<Action> Refreshers = new();
	private static bool _refreshing;

	internal static void Install(UiScope scope) => _scope = scope;

	internal static void Toggle(LobbyGroup heading) {
		if (heading == null) return;
		bool same = _win != null && _key == heading.Key;
		Close();
		if (!same) Open(heading);
	}

	internal static void Close() => _win?.Close();

	private static void Open(LobbyGroup heading) {
		var rules = LobbyRulesScreen.Others(heading.Rules.Where(LobbyRulesScreen.CanShow).ToList());
		if (rules.Count == 0 || _scope == null) return;
		_key = heading.Key;
		_win = _scope.Custom.Window(w => {
			w.Title(heading.Title).Size(560f, Mathf.Min(780f, 150f + rules.Count * 78f)).Position(120f, 60f).Draggable().Closable();
			w.OnClose(() => { _win = null; _key = null; Refreshers.Clear(); });
			w.Body.Spacing(4f);
			var status = w.Body.Paragraph("");
			Refreshers.Add(() => {
				if (status == null) return;
				status.Value = !rules.Any(r => r.Visible) ? Tx.T("rules.nothingNow") : rules.Any(r => r.Editable) ? "" : Tx.T("rules.hostOnly");
			});
			w.Body.Scroll(c => {
				c.Spacing(4f);
				foreach (var rule in rules) Add(c, rule, LobbyRulesScreen.WindowLabel(rule, heading));
			})?.FillWidth().FillHeight();
		});
		Refresh();
	}

	private static void Add(UiContainer c, ILobbyRule rule, string label) {
		var parts = new List<UiElement>();
		switch (rule) {
			case LobbyRule<bool> toggle: {
				var t = c.Toggle(label, toggle.Live, v => { if (_refreshing) return; toggle.TrySet(v); LobbyRulesScreen.Refresh(); });   // snaps back when refused
				parts.Add(t);
				Refreshers.Add(() => t?.Set(toggle.Live).Interactable(toggle.Editable));
				break;
			}
			case LobbyRule<int> choice when choice.Choices != null: {
				UiDropdown d = null;
				parts.Add(c.Row(r => {
					r.Spacing(6f);
					r.Text(label)?.FillWidth();
					d = r.Dropdown(choice.Choices, choice.Live, i => { if (_refreshing) return; choice.TrySet(i); LobbyRulesScreen.Refresh(); })?.FixedW(220f);
				})?.Height(34f));
				Refreshers.Add(() => d?.Set(choice.Live).Interactable(choice.Editable));
				break;
			}
			case LobbyRule<int> number: {
				UiText value = null;
				var edits = new List<UiButton>();
				parts.Add(c.Row(r => {
					r.Spacing(4f);
					r.Text(label)?.FillWidth();
					edits.Add(r.Button("-", () => Step(number, -number.Step))?.FixedW(36f));
					value = r.Text("")?.FixedW(70f);
					edits.Add(r.Button("+", () => Step(number, number.Step))?.FixedW(36f));
					edits.Add(r.Button(Tx.T("rules.default"), () => { number.TrySet(number.Default); LobbyRulesScreen.Refresh(); })?.FixedW(110f));
				})?.Height(32f));
				Refreshers.Add(() => {
					if (value != null) {
						value.Value = number.Live.ToString(CultureInfo.InvariantCulture);
						value.Color(number.Live == number.Default ? Normal : Changed);
					}
					foreach (var b in edits) b?.Interactable(number.Editable);
				});
				break;
			}
			default:
				return;
		}
		if (!string.IsNullOrWhiteSpace(rule.Description)) parts.Add(c.Paragraph(rule.Description)?.Size(16f).Color(Dim));
		LobbyRulesScreen.Subscribe(rule);   // a change from the host redraws the window
		Refreshers.Add(() => {
			foreach (var p in parts)
				if (p?.Root != null && p.Root.gameObject.activeSelf != rule.Visible) p.Root.gameObject.SetActive(rule.Visible);
		});
	}

	private static void Step(LobbyRule<int> rule, int delta) {
		int next = Math.Clamp(rule.Live + delta, rule.Min, rule.Max);
		if (next != rule.Live) rule.TrySet(next);
		LobbyRulesScreen.Refresh();
	}

	internal static void Refresh() {
		if (_win == null || _refreshing) return;
		_refreshing = true;
		try {
			foreach (var r in Refreshers) {
				try { r(); }
				catch (Exception e) { Plugin.Log.LogWarning("could not update a lobby rules window row: " + e.Message); }
			}
		}
		finally { _refreshing = false; }
	}
}
