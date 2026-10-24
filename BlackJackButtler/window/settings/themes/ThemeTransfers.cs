using System;
using System.IO;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void OpenThemeImportDialog()
    {
        _fileDialogManager.OpenFileDialog("Import Theme", "JSON Files{.json}", (ok, path) =>
        {
            if (!ok || string.IsNullOrWhiteSpace(path)) return;
            RunThemeAction(() =>
            {
                var json = File.ReadAllText(path);
                AddTheme(_themes.Import(json, Path.GetFileNameWithoutExtension(path)));
            });
        });
    }

    private void OpenThemeExportDialog(string name)
    {
        var json = _themes.Export(name);
        _fileDialogManager.SaveFileDialog("Export Theme", "JSON Files{.json}", name, ".json", (ok, path) =>
        {
            if (!ok || string.IsNullOrWhiteSpace(path)) return;
            RunThemeAction(() => File.WriteAllText(path, json));
        });
    }
}
