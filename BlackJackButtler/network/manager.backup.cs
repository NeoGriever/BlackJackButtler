using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

[Serializable]
public class SessionSnapshot
{
    public DateTime Timestamp { get; set; }
    public GamePhase CurrentPhase { get; set; }
    public List<PlayerState> Players { get; set; } = new();
    public PlayerState Dealer { get; set; } = new();
    public List<(int Index, GameSnapshot Snapshot)> GameHistory { get; set; } = new();
    public int CurrentHistoryIndex { get; set; }
    public bool IsRecognitionActive { get; set; }
}

public static class SessionManager
{
    private static string GetSessionFilePath()
    {
        var pluginDir = Plugin.PluginInterface.GetPluginConfigDirectory();
        return Path.Combine(pluginDir, "active_session.json");
    }

    public static void SaveSession(
        List<PlayerState> players,
        PlayerState dealer,
        GamePhase phase,
        bool isRecognitionActive)
    {
        if (!isRecognitionActive)
        {
            ClearSession();
            return;
        }

        if (phase == GamePhase.Waiting || phase == GamePhase.InitialDeal)
        {
            return;
        }

        try
        {
            var snapshot = new SessionSnapshot
            {
                Timestamp = DateTime.UtcNow,
                CurrentPhase = phase,
                Players = players.Select(p => p.Clone()).ToList(),
                Dealer = dealer.Clone(),
                GameHistory = GameLog.GetAllSnapshots(),
                CurrentHistoryIndex = GameLog.CurrentIndex,
                IsRecognitionActive = isRecognitionActive
            };

            var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });

            File.WriteAllText(GetSessionFilePath(), json);

            Plugin.Log.Debug($"[SessionManager] Session saved - Phase: {phase}, Players: {players.Count}, Timestamp: {snapshot.Timestamp}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[SessionManager] Failed to save session: {ex.Message}");
        }
    }

    public static SessionSnapshot? LoadSession()
    {
        var path = GetSessionFilePath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var snapshot = JsonConvert.DeserializeObject<SessionSnapshot>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });

            if (snapshot != null)
            {
                Plugin.Log.Information($"[SessionManager] Session loaded - Phase: {snapshot.CurrentPhase}, Age: {(DateTime.UtcNow - snapshot.Timestamp).TotalMinutes:F1} minutes");
            }

            return snapshot;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[SessionManager] Failed to load session: {ex.Message}");
            return null;
        }
    }

    public static bool HasSavedSession()
    {
        var path = GetSessionFilePath();
        return File.Exists(path);
    }

    public static void ClearSession()
    {
        var path = GetSessionFilePath();
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                Plugin.Log.Debug("[SessionManager] Session cleared");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"[SessionManager] Failed to clear session: {ex.Message}");
            }
        }
    }

    public static bool RestoreSession(
        out List<PlayerState> players,
        out PlayerState dealer,
        out GamePhase phase,
        out List<(int, GameSnapshot)> history,
        out int currentHistoryIndex)
    {
        players = new List<PlayerState>();
        dealer = new PlayerState { Name = "Dealer", IsActivePlayer = true };
        phase = GamePhase.Waiting;
        history = new List<(int, GameSnapshot)>();
        currentHistoryIndex = 0;

        var snapshot = LoadSession();
        if (snapshot == null)
            return false;

        try
        {
            players = snapshot.Players;
            dealer = snapshot.Dealer;
            phase = snapshot.CurrentPhase;
            history = snapshot.GameHistory;
            currentHistoryIndex = snapshot.CurrentHistoryIndex;

            Plugin.Log.Information($"[SessionManager] Session restored successfully - {players.Count} players, Phase: {phase}");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[SessionManager] Failed to restore session: {ex.Message}");
            return false;
        }
    }
}
