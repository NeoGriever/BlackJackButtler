using System;
using System.Collections.Generic;

namespace BlackJackButtler.Chat;

public class SessionVariable
{
    public string Name = "";
    public string Value = "";
    public bool IsManual = false;
}

public static class VariableManager
{
    public static List<SessionVariable> Variables = new();
    private static readonly object _lock = new();

    public static void SetVariable(string name, string value)
    {
        lock (_lock)
        {
            var existing = Variables.Find(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                existing.Value = value;
            else
                Variables.Add(new SessionVariable { Name = name, Value = value });
        }
    }

    public static void SetPlayerVariables(PlayerState p)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        SetVariable("bankamount", p.Bank.ToString("N0", culture) + " Gil");
        SetVariable("betamount", p.CurrentBet.ToString("N0", culture) + " Gil");
        SetVariable("lastwin", p.LastRoundResult.ToString("N0", culture) + " Gil");
    }

    public static List<SessionVariable> SnapshotForUi()
    {
        lock (_lock)
        {
            return new List<SessionVariable>(Variables);
        }
    }

    public static void AddManual(SessionVariable v)
    {
        lock (_lock)
        {
            Variables.Add(v);
        }
    }

    public static void RemoveAt(int index)
    {
        lock (_lock)
        {
            if (index >= 0 && index < Variables.Count)
                Variables.RemoveAt(index);
        }
    }

    public static string ProcessMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;

        List<SessionVariable> snapshot;
        lock (_lock)
        {
            snapshot = new List<SessionVariable>(Variables);
        }

        string result = message;

        foreach (var v in snapshot)
        {
            string placeholder = "$${" + v.Name + "}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, v.Value);
                lock (_lock) { v.Value = ""; }
            }
        }

        foreach (var v in snapshot)
        {
            string placeholder = "${" + v.Name + "}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, v.Value);
            }
        }

        return result;
    }
}
