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
    private List<BetLimitEntry>? _betDraftEntries;
    private readonly List<List<ShortResultRule>> _shortResultUndoHistory = new();
    private string _shortResultImportStatus = string.Empty;
    private bool _nearbyCooldownEditMode;
    private string _nearbyCooldownInput = string.Empty;
    private string _nearbySoundTestStatus = string.Empty;
    private bool _nearbySoundTestFailed;
}
