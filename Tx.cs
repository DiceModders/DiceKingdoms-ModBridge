using DiceModders.ModCore;

namespace DiceModders.ModBridge;

/// <summary>
/// This mod's texts (texts.json built into the DLL, a DiceModders.ModBridge.texts.json next to the DLL wins line by line) in the game's language,
/// through ModCore: the exact language, its base, then English. Ask at draw time, so a language change shows on the next redraw.
/// </summary>
internal static class Tx
{
    internal static TextSet Set;
    internal static void Load() => Set = DiceModders.ModCore.ModCore.Texts.Load();
    internal static string T(string key) => Set?.Get(key) ?? key;
    internal static string T(string key, params object[] values) => Set?.Get(key, values) ?? key;
}
