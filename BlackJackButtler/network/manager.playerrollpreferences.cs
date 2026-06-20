using System;

namespace BlackJackButtler;

public static class PlayerRollPreferenceManager
{
    public static bool ShouldPlayerRoll(Configuration cfg, PlayerState player)
        => cfg.PlayerRollingForThemselves && GetPreference(cfg, player);

    public static bool GetPreference(Configuration cfg, PlayerState player)
    {
        cfg.PlayerSelfRollPreferences ??= new();
        var exactKey = GetKey(player.Name, player.WorldId);
        if (cfg.PlayerSelfRollPreferences.TryGetValue(exactKey, out var enabled))
            return enabled;

        // Preserve preferences saved before the player's home world was resolved.
        var nameOnlyKey = GetKey(player.Name, 0);
        return !cfg.PlayerSelfRollPreferences.TryGetValue(nameOnlyKey, out enabled) || enabled;
    }

    public static void SetPreference(Configuration cfg, PlayerState player, bool enabled)
    {
        cfg.PlayerSelfRollPreferences ??= new();
        cfg.PlayerSelfRollPreferences[GetKey(player.Name, player.WorldId)] = enabled;
    }

    private static string GetKey(string name, uint worldId)
        => $"{worldId}:{name.Trim().ToUpperInvariant()}";
}
