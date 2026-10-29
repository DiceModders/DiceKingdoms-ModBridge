using System.Collections.Generic;
using DiceModders.ModCore;
using DiceModders.UiKit;
using DiceModders.UiKit.Native;
using Core = DiceModders.ModCore.ModCore;   // the class and its namespace share a name

namespace DiceModders.ModBridge;

/// <summary>An entry in the Rules screen's civilization drop-down for every civilization registered with ModCore.</summary>
internal static class CivilizationEntries {
	private static readonly Dictionary<int, RulesCivilizationHandle> Entries = new();
	private static UiScope _scope;

	public static void Install(UiScope scope) {
		_scope = scope;
		foreach (var civ in Core.Civilizations.All) Add(civ);
		Core.Civilizations.Added += Add;
		Core.Civilizations.Removed += Remove;
	}

	private static void Add(ModCivilization civ) {
		if (Entries.Remove(civ.Id, out var old)) old.Dispose();
		var entry = _scope.Native.Rules.AddCivilization(civ.Id, civ.Name);
		if (entry != null) Entries[civ.Id] = entry;
	}

	private static void Remove(ModCivilization civ) {
		if (!Entries.Remove(civ.Id, out var entry)) return;
		entry.Dispose();
	}
}