using System;
using System.Collections.Generic;

namespace BlackJackButtler.Chat;

public class SessionVariable
{
    // Als Felder definiert für ref-Zugriff
    public string Name = "";
    public string Value = "";
    public bool IsManual = false;
}

public static class VariableManager
{
    public static List<SessionVariable> Variables = new();

    public static void SetVariable(string name, string value)
    {
        var existing = Variables.Find(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            existing.Value = value;
        else
            Variables.Add(new SessionVariable { Name = name, Value = value });
    }

    public static string ProcessMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;

        string result = message;

        // 1. $${var} (Wert einsetzen + danach leeren)
        foreach (var v in Variables)
        {
            string placeholder = "$${" + v.Name + "}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, v.Value);
                v.Value = "";
            }
        }

        // 2. ${var} (Nur Wert einsetzen)
        foreach (var v in Variables)
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
