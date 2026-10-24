using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void ExportConfig()
    {
        var exportObj = JObject.FromObject(_config);
        exportObj.Remove("Presets");
        var json = exportObj.ToString(Newtonsoft.Json.Formatting.Indented);
        _fileDialogManager.SaveFileDialog(
            "Export Config",
            "JSON Files{.json}",
            "bjb_config",
            ".json",
            (ok, path) =>
            {
                if (ok && !string.IsNullOrWhiteSpace(path))
                    System.IO.File.WriteAllText(path, json);
            }
        );
    }

    private void ImportConfig()
    {
        _fileDialogManager.OpenFileDialog(
            "Import Config",
            "JSON Files{.json}",
            (ok, path) =>
            {
                if (!ok || string.IsNullOrWhiteSpace(path)) return;
                try
                {
                    var json = System.IO.File.ReadAllText(path);
                    _tempImportJson = JObject.Parse(json);
                    if (!_tempImportJson.ContainsKey("UnlockWaitTimer")
                        || !_tempImportJson.ContainsKey("EnableBankInput")
                        || !_tempImportJson.ContainsKey("NotepadText"))
                        DoMerge();
                    else
                        _openImportConfirmPopup = true;
                }
                catch { }
            }
        );
    }
}
