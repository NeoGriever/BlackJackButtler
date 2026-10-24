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
    private void DrawNearbySettingsContent()
    {
        DrawSettingsToggle("Enabled", "nearby_enabled", ref _config.ShowNearbyPlayers);
        IntInputSave("Columns##nearby_columns", ref _config.NearbyColumns, 1, 5, 1, 2);
        Header("Sound");
        Indent(() => DrawNearbySoundSettings("v2"));
    }
}
