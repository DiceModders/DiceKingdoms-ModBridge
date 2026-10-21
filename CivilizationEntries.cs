using DiceModders.ModCore;
using DiceModders.UiKit;
using Core = DiceModders.ModCore.ModCore;   // the class and its namespace share a name
using Ui = DiceModders.UiKit.UiKit;

namespace DiceModders.ModBridge;

/// <summary>An entry in the Rules screen's civilization drop-down for every civilization registered with ModCore.</summary>
internal static class CivilizationEntries {
	private static readonly System.Collections.Generic.Dictionary<int, RulesCivilizationEntry> Entries = new();

	public static void Install() {
		foreach (var civ in Core.Civilizations.All) Add(civ);
		Core.Civilizations.Added += Add;
		Core.Civilizations.Removed += Remove;
	}

	private static void Add(ModCivilization civ) {
		if (Entries.TryGetValue(civ.Id, out var old)) old.Remove();
		Entries[civ.Id] = Ui.Rules.AddCivilizationEntry(civ.Id, civ.Name);
	}

	private static void Remove(ModCivilization civ) {
		if (!Entries.Remove(civ.Id, out var entry)) return;
		entry.Remove();
	}
}
