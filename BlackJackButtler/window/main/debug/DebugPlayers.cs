using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void CreateTestData()
    {
        GenerateRandomDebugPlayers();
    }

    private void GenerateRandomDebugPlayers()
    {
        Plugin.ResetDebugDiceSequence();

        var existingDebugCount = _players.Count(p => p.IsDebugPlayer);
        var count = Math.Max(0, 7 - existingDebugCount);
        if (count == 0)
            return;

        var worlds = WorldNameManager.SortedWorldNames.ToArray();
        var activeExisting = _players.Count(p => p.IsDebugPlayer && p.IsActivePlayer);
        for (var i = 0; i < count; i++)
        {
            var world = worlds[Random.Shared.Next(worlds.Length)];
            var token = Random.Shared.Next(0x10000000, int.MaxValue).ToString("X8");
            var active = activeExisting + i < 2;
            _players.Add(
                new PlayerState
                {
                    Name = $"Player {token}@{world}",
                    WorldId = DebugWorldId(world),
                    IsActivePlayer = active,
                    IsDebugPlayer = true,
                    IsInParty = true,
                    IsCurrentTurn = false,
                    Bank = active ? Random.Shared.Next(100, 1000) : Random.Shared.Next(50, 501) * 1000L,
                    CurrentBet = active ? 100 : Random.Shared.Next(1, 6) * 1000L,
                }
            );
        }

        CompanionSyncManager.SendPlayersUpdate(_config, _players.Where(p => p.IsDebugPlayer));
        GameEngine.SetRuntimeContext(_players, _dealer);
        AddDebugLog($"[DEBUG] Added {count} random debug players ({existingDebugCount + count}/7).", false);
        _save();
    }

    private static uint DebugWorldId(string world)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in world)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return hash == 0 ? 1 : hash;
        }
    }
}
