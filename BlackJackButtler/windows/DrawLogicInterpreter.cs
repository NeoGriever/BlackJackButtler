using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace BlackJackButtler.Windows;

public sealed class DrawLogicContext
{
    public Vector3 Position;
    public string PlayerName = "";
    public bool IsDealer;
    public int Score;
    public string Cards = "";
    public int CardCount;
    public long Bank;
    public long Bet;
    public int HandIndex;
    public int HandCount;
    public float Rotation;
    public bool IsFocused;
    public bool IsNearby;
    public bool IsVisible;
    public bool IsOnline;
    public bool IsInGroup;
    public bool GroupExists;
    public bool IsBust;
    public bool IsStand;
    public bool IsBlackjack;
    public bool IsCharlie;
    public bool IsDone;
    public bool IsDoubleDown;

    public PlayerState? SourcePlayer;
    public Configuration? Config;
    public string WorldName = "";
    public bool IsPlaying;
    public long MaxBet;

    public int IterHandIndex = -1;
    public int IterHandCount = -1;
    public int IterHandPoints;
    public int IterHandPointsB;
    public bool IterHandBusted;
    public bool IterHandActive;

    public int IterCardIndex = -1;
    public int IterCardCount = -1;
    public int IterCardNumber;
    public int IterCardColor;
    public DateTime IterCardDrawnAt;

    public int IterLetterIndex = -1;
    public int IterLetterCount = -1;
    public string IterLetter = "";

    public float DealerDirection;
    public float CameraDirection;
}

public static class DrawLogicInterpreter
{
    private const int MaxDepth = 10;

    private static WorldDrawing? _drawing;
    private static Shape? _currentShape;
    private static List<PlayerState>? _execPlayers;
    private static PlayerState? _execDealer;
    private static Configuration? _execConfig;

    private static readonly Dictionary<string, float> _vars = new();
    private static readonly List<Shape> _pendingDraws = new();
    private static DrawLogicContext? _evalCtx;
    private static Vector3 _cachedDealerPos;
    private static float _cachedCameraDirection;
    private static Vector3 _cachedCameraPosition;

    private static readonly List<string> _debugLog = new();
    private static bool _debugCapturing;
    public static IReadOnlyList<string> DebugLog => _debugLog;
    public static bool HasDebugLog => _debugLog.Count > 0;
    public static void TriggerDebugCapture() { _debugCapturing = true; }
    public static void ClearDebugLog() { _debugLog.Clear(); }

    public static void ExecuteStartEntry(List<DrawLogicEntry> entries, string startName,
        List<PlayerState> players, PlayerState dealer, Configuration config)
    {
        if (string.IsNullOrEmpty(startName)) return;
        var startEntry = entries.FirstOrDefault(e => e.Name == startName);
        if (startEntry == null) return;
        if (!startEntry.IsActive) return;

        _drawing = new WorldDrawing();
        _currentShape = null;
        _execPlayers = players;
        _execDealer = dealer;
        _execConfig = config;
        _cachedDealerPos = GetDealerPosition();
        _cachedCameraDirection = GetCameraDirection();
        _cachedCameraPosition = GetCameraPosition();

        if (_debugCapturing)
        {
            _debugLog.Clear();
            _debugLog.Add($"DealerPos=({_cachedDealerPos.X:F2}, {_cachedDealerPos.Y:F2}, {_cachedDealerPos.Z:F2})  CamDir={_cachedCameraDirection:F4}");
        }

        try
        {
            if (startEntry.IsIterate)
            {
                RunIterated(startEntry.Script, entries, players, dealer, config, 0);
            }
            else
            {
                var ctx = BuildContextNoPlayer(config);
                Execute(startEntry.Script, ctx, entries, 0);
            }

            FlushPendingDraws();
        }
        finally
        {
            _pendingDraws.Clear();
            _debugCapturing = false;
            _execPlayers = null;
            _execDealer = null;
            _execConfig = null;
        }
    }

    private static void FlushPendingDraws()
    {
        if (_pendingDraws.Count == 0) return;

        var camPos = _cachedCameraPosition;

        _pendingDraws.Sort((a, b) =>
        {
            var da = Vector3.DistanceSquared(a.Origin, camPos);
            var db = Vector3.DistanceSquared(b.Origin, camPos);
            return db.CompareTo(da);
        });

        foreach (var shape in _pendingDraws)
            shape.Draw();

        _pendingDraws.Clear();
    }

    private static void RunIterated(string script, List<DrawLogicEntry> entries,
        List<PlayerState> players, PlayerState dealer, Configuration config, int depth)
    {
        var active = players.Where(p => p.IsActivePlayer && !p.IsOnHold && !p.IsOnBench).ToList();
        foreach (var player in active)
        {
            var ctx = BuildContext(player, false, config);
            if (_debugCapturing)
                _debugLog.Add($"\n--- Player: {ctx.PlayerName} ---\n  Pos=({ctx.Position.X:F2}, {ctx.Position.Y:F2}, {ctx.Position.Z:F2})  Rot={ctx.Rotation:F4}  DealerDir={ctx.DealerDirection:F4}  CamDir={ctx.CameraDirection:F4}");
            Execute(script, ctx, entries, depth);
        }
        var dealerCtx = BuildContext(dealer, true, config);
        if (_debugCapturing)
            _debugLog.Add($"\n--- Dealer: {dealerCtx.PlayerName} ---\n  Pos=({dealerCtx.Position.X:F2}, {dealerCtx.Position.Y:F2}, {dealerCtx.Position.Z:F2})  Rot={dealerCtx.Rotation:F4}  DealerDir={dealerCtx.DealerDirection:F4}  CamDir={dealerCtx.CameraDirection:F4}");
        Execute(script, dealerCtx, entries, depth);
    }

    private static DrawLogicContext BuildContext(PlayerState player, bool isDealer, Configuration config)
    {
        var ctx = new DrawLogicContext
        {
            PlayerName = player.DisplayName,
            IsDealer = isDealer,
            Bank = player.Bank,
            Bet = player.CurrentBet,
            HandIndex = player.CurrentHandIndex,
            HandCount = player.Hands.Count,
            IsFocused = player.IsCurrentTurn,
            IsInGroup = player.IsInParty,
            GroupExists = Plugin.PartyList.Length > 0,
            IsDone = player.IsDone,
            SourcePlayer = player,
            Config = config,
            WorldName = VipManager.ResolveWorldName(player.WorldId),
            IsPlaying = player.IsActivePlayer && !player.IsOnHold && !player.IsOnBench,
            MaxBet = player.GetEffectiveMaxBet(config),
        };

        if (player.Hands.Count > 0 && player.CurrentHandIndex < player.Hands.Count)
        {
            var hand = player.Hands[player.CurrentHandIndex];
            ctx.Score = player.GetBestScore(player.CurrentHandIndex);
            ctx.Cards = player.GetCardsString(player.CurrentHandIndex);
            ctx.CardCount = hand.Cards.Count;
            ctx.IsBust = hand.IsBust;
            ctx.IsStand = hand.IsStand;
            ctx.IsBlackjack = hand.IsNaturalBlackJack;
            ctx.IsCharlie = hand.IsCharlie;
            ctx.IsDoubleDown = hand.IsDoubleDown;
        }

        if (isDealer)
        {
            var local = Plugin.ObjectTable.LocalPlayer;
            if (local != null)
            {
                ctx.Position = local.Position;
                ctx.Rotation = local.Rotation;
                ctx.IsOnline = true;
                ctx.IsVisible = Plugin.GameGui.WorldToScreen(local.Position, out _);
            }
        }
        else
        {
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj.ObjectKind != ObjectKind.Pc) continue;
                if (obj is not IPlayerCharacter pc) continue;
                if (pc.Name.TextValue == player.Name)
                {
                    ctx.Position = pc.Position;
                    ctx.Rotation = pc.Rotation;
                    ctx.IsOnline = true;
                    ctx.IsVisible = Plugin.GameGui.WorldToScreen(pc.Position, out _);
                    break;
                }
            }
        }

        ctx.DealerDirection = ComputeDirection(ctx.Position, _cachedDealerPos);
        ctx.CameraDirection = _cachedCameraDirection;

        var nearby = NearbyPlayersManager.GetNearbyPlayers(config);
        ctx.IsNearby = nearby.Any(n => n.Name == player.Name && n.IsInRange);

        return ctx;
    }

    private static DrawLogicContext BuildContextNoPlayer(Configuration config)
    {
        var ctx = new DrawLogicContext
        {
            GroupExists = Plugin.PartyList.Length > 0,
            Config = config,
        };

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local != null)
        {
            ctx.Position = local.Position;
            ctx.Rotation = local.Rotation;
            ctx.IsOnline = true;
            ctx.IsVisible = true;
            ctx.PlayerName = local.Name.TextValue;
        }

        ctx.DealerDirection = ComputeDirection(ctx.Position, _cachedDealerPos);
        ctx.CameraDirection = _cachedCameraDirection;

        return ctx;
    }

    public static void Execute(string script, DrawLogicContext ctx, List<DrawLogicEntry> allEntries, int depth)
    {
        if (depth > MaxDepth) return;
        if (string.IsNullOrWhiteSpace(script)) return;

        _drawing ??= new WorldDrawing();

        var lines = script.Split('\n');
        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
            {
                i++;
                continue;
            }

            if (line.StartsWith("IterateHand") && line.TrimEnd().EndsWith("{"))
            {
                var (body, endIdx) = ExtractBlock(lines, i);
                if (body != null)
                    ExecuteIterateHand(body, ctx, allEntries, depth);
                i = endIdx + 1;
                continue;
            }

            if (line.StartsWith("IterateCard") && line.TrimEnd().EndsWith("{"))
            {
                var (body, endIdx) = ExtractBlock(lines, i);
                if (body != null)
                    ExecuteIterateCard(body, ctx, allEntries, depth);
                i = endIdx + 1;
                continue;
            }

            if (line.StartsWith("IterateLetter") && line.TrimEnd().EndsWith("{"))
            {
                var (body, endIdx) = ExtractBlock(lines, i);
                if (body != null)
                    ExecuteIterateLetter(body, ctx, allEntries, depth);
                i = endIdx + 1;
                continue;
            }

            if (line.StartsWith("if ") && line.TrimEnd().EndsWith("{"))
            {
                var resolvedLine = ReplaceTokens(line, ctx);
                var (body, endIdx) = ExtractBlock(lines, i);
                if (body != null && EvaluateIfCondition(resolvedLine))
                    Execute(body, ctx, allEntries, depth);
                i = endIdx + 1;
                continue;
            }

            _evalCtx = ctx;
            var rawLine = line;
            line = ReplaceTokens(line, ctx);

            if (TryParseFunctionCall(line, out var funcName, out var args))
            {
                if (_debugCapturing)
                {
                    if (rawLine != line)
                        _debugLog.Add($"  {rawLine}\n    → {funcName}({string.Join(", ", args)})");
                    else
                        _debugLog.Add($"  {funcName}({string.Join(", ", args)})");
                }
                ExecuteFunction(funcName, args, ctx, allEntries, depth);
            }

            i++;
        }
    }

    private static (string? body, int endIndex) ExtractBlock(string[] lines, int startLine)
    {
        int depth = 0;
        int bodyStart = startLine + 1;

        for (int i = startLine; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            foreach (char c in trimmed)
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
            }
            if (depth <= 0 && i > startLine)
            {
                var bodyLines = new List<string>();
                for (int j = bodyStart; j < i; j++)
                    bodyLines.Add(lines[j]);
                var closingTrimmed = lines[i].Trim();
                if (closingTrimmed != "}")
                {
                    int bracePos = closingTrimmed.LastIndexOf('}');
                    if (bracePos > 0)
                        bodyLines.Add(closingTrimmed.Substring(0, bracePos));
                }
                return (string.Join('\n', bodyLines), i);
            }
        }
        return (null, lines.Length - 1);
    }

    private static void ExecuteIterateHand(string body, DrawLogicContext ctx,
        List<DrawLogicEntry> allEntries, int depth)
    {
        var player = ctx.SourcePlayer;
        if (player == null) return;

        for (int h = 0; h < player.Hands.Count; h++)
        {
            var handCtx = CloneContext(ctx);
            handCtx.IterHandIndex = h;
            handCtx.IterHandCount = player.Hands.Count;

            var (min, max) = player.CalculatePoints(h);
            int best = (max.HasValue && max.Value <= 21) ? max.Value : min;
            handCtx.IterHandPoints = best;
            handCtx.IterHandPointsB = (max.HasValue && max.Value != min) ? min : 0;
            handCtx.IterHandBusted = min > 21;
            handCtx.IterHandActive = player.IsCurrentTurn && player.CurrentHandIndex == h;

            Execute(body, handCtx, allEntries, depth);
        }
    }

    private static void ExecuteIterateCard(string body, DrawLogicContext ctx,
        List<DrawLogicEntry> allEntries, int depth)
    {
        var player = ctx.SourcePlayer;
        if (player == null) return;

        int handIdx = ctx.IterHandIndex >= 0 ? ctx.IterHandIndex : player.CurrentHandIndex;
        if (handIdx < 0 || handIdx >= player.Hands.Count) return;
        var cards = player.Hands[handIdx].Cards;

        for (int c = 0; c < cards.Count; c++)
        {
            var card = cards[c];
            var cardCtx = CloneContext(ctx);
            cardCtx.IterCardIndex = c;
            cardCtx.IterCardCount = cards.Count;
            cardCtx.IterCardNumber = card.Value;
            cardCtx.IterCardColor = MapSuitToUser(card.Suit);
            cardCtx.IterCardDrawnAt = card.DrawnAt;

            Execute(body, cardCtx, allEntries, depth);
        }
    }

    private static void ExecuteIterateLetter(string body, DrawLogicContext ctx,
        List<DrawLogicEntry> allEntries, int depth)
    {
        if (ctx.IterCardIndex < 0) return;

        string label = ctx.IterCardNumber switch
        {
            1 => "A", 11 => "J", 12 => "Q", 13 => "K",
            _ => ctx.IterCardNumber.ToString()
        };

        for (int l = 0; l < label.Length; l++)
        {
            var letterCtx = CloneContext(ctx);
            letterCtx.IterLetterIndex = l;
            letterCtx.IterLetterCount = label.Length;
            letterCtx.IterLetter = label[l].ToString();
            Execute(body, letterCtx, allEntries, depth);
        }
    }

    private static int MapSuitToUser(CardSuit suit)
    {
        return suit switch
        {
            CardSuit.Spades => 0,
            CardSuit.Clubs => 1,
            CardSuit.Hearts => 2,
            CardSuit.Diamonds => 3,
            _ => 0,
        };
    }

    private static Vector4 GetSuitColor(int userSuit, Configuration? config)
    {
        if (config == null) return new Vector4(1, 1, 1, 1);
        return userSuit switch
        {
            0 => config.DrawLogicColorSpades,
            1 => config.DrawLogicColorClubs,
            2 => config.DrawLogicColorHearts,
            3 => config.DrawLogicColorDiamonds,
            _ => new Vector4(1, 1, 1, 1),
        };
    }

    public static string ReplaceTokens(string line, DrawLogicContext ctx)
    {
        line = line.Replace("<pos>.x", ctx.Position.X.ToString("F4", CultureInfo.InvariantCulture));
        line = line.Replace("<pos>.y", ctx.Position.Y.ToString("F4", CultureInfo.InvariantCulture));
        line = line.Replace("<pos>.z", ctx.Position.Z.ToString("F4", CultureInfo.InvariantCulture));

        line = line.Replace("<IsCurrentTurn>", ctx.IsFocused ? "1" : "0");
        line = line.Replace("<IsPlaying>", ctx.IsPlaying ? "1" : "0");

        line = line.Replace("<BankF>", ctx.Bank.ToString("N0", CultureInfo.GetCultureInfo("en-US")));
        line = line.Replace("<BetF>", ctx.Bet.ToString("N0", CultureInfo.GetCultureInfo("en-US")));
        line = line.Replace("<MaxBetF>", ctx.MaxBet.ToString("N0", CultureInfo.GetCultureInfo("en-US")));
        line = line.Replace("<MaxBet>", ctx.MaxBet.ToString(CultureInfo.InvariantCulture));

        line = line.Replace("<NameW>", ctx.PlayerName + "@" + ctx.WorldName);

        if (ctx.Config != null)
        {
            line = line.Replace("<Scale>", ctx.Config.DrawLogicScale.ToString("F4", CultureInfo.InvariantCulture));
            line = line.Replace("<OffsetX>", ctx.Config.DrawLogicOffsetX.ToString("F4", CultureInfo.InvariantCulture));
            line = line.Replace("<OffsetY>", ctx.Config.DrawLogicOffsetY.ToString("F4", CultureInfo.InvariantCulture));
            line = line.Replace("<OffsetZ>", ctx.Config.DrawLogicOffsetZ.ToString("F4", CultureInfo.InvariantCulture));
            line = line.Replace("<OffsetR>", ctx.Config.DrawLogicOffsetR.ToString("F4", CultureInfo.InvariantCulture));
        }

        line = line.Replace("<HandsTotal>", ctx.IterHandCount >= 0 ? ctx.IterHandCount.ToString(CultureInfo.InvariantCulture) : "0");
        line = line.Replace("<HandPointsB>", ctx.IterHandPointsB.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<HandPoints>", ctx.IterHandPoints.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<HandIndex>", ctx.IterHandIndex >= 0 ? ctx.IterHandIndex.ToString(CultureInfo.InvariantCulture) : ctx.HandIndex.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<HandActive>", ctx.IterHandActive ? "1" : "0");
        line = line.Replace("<HandBusted>", ctx.IterHandBusted ? "1" : "0");

        if (ctx.IterCardIndex >= 0)
        {
            var suitColor = GetSuitColor(ctx.IterCardColor, ctx.Config);
            line = line.Replace("<CardsTotal>", ctx.IterCardCount.ToString(CultureInfo.InvariantCulture));
            line = line.Replace("<CardColorR>", suitColor.X.ToString("F4", CultureInfo.InvariantCulture));
            line = line.Replace("<CardColorG>", suitColor.Y.ToString("F4", CultureInfo.InvariantCulture));
            line = line.Replace("<CardColorB>", suitColor.Z.ToString("F4", CultureInfo.InvariantCulture));
            line = line.Replace("<CardColor>", ctx.IterCardColor.ToString(CultureInfo.InvariantCulture));
            line = line.Replace("<CardNumber>", ctx.IterCardNumber.ToString(CultureInfo.InvariantCulture));
            line = line.Replace("<CardIndex>", ctx.IterCardIndex.ToString(CultureInfo.InvariantCulture));

            double ageSec = ctx.IterCardDrawnAt == DateTime.MinValue
                ? 3.0
                : (DateTime.UtcNow - ctx.IterCardDrawnAt).TotalSeconds;
            float cardAge = (float)Math.Clamp(ageSec / 3.0, 0.0, 1.0);
            line = line.Replace("<CardAge>", cardAge.ToString("F4", CultureInfo.InvariantCulture));

            if (ctx.IterLetterIndex >= 0)
            {
                line = line.Replace("<letterTotal>", ctx.IterLetterCount.ToString(CultureInfo.InvariantCulture));
                line = line.Replace("<letterIndex>", ctx.IterLetterIndex.ToString(CultureInfo.InvariantCulture));
                line = line.Replace("<letter>", ctx.IterLetter);
            }
        }

        line = line.Replace("<name>", ctx.PlayerName);
        line = line.Replace("<cards>", ctx.Cards);

        line = line.Replace("<score>", ctx.Score.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<cardcount>", ctx.CardCount.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<bank>", ctx.Bank.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<bet>", ctx.Bet.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<handindex>", ctx.HandIndex.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<handcount>", ctx.HandCount.ToString(CultureInfo.InvariantCulture));
        line = line.Replace("<rotation>", ctx.Rotation.ToString("F4", CultureInfo.InvariantCulture));
        line = line.Replace("<userRot>", ctx.Rotation.ToString("F4", CultureInfo.InvariantCulture));
        line = line.Replace("<dealerDirection>", ctx.DealerDirection.ToString("F4", CultureInfo.InvariantCulture));
        line = line.Replace("<cameraDirection>", ctx.CameraDirection.ToString("F4", CultureInfo.InvariantCulture));

        line = line.Replace("<isdealer>", ctx.IsDealer ? "1" : "0");
        line = line.Replace("<focused>", ctx.IsFocused ? "1" : "0");
        line = line.Replace("<nearby>", ctx.IsNearby ? "1" : "0");
        line = line.Replace("<visible>", ctx.IsVisible ? "1" : "0");
        line = line.Replace("<online>", ctx.IsOnline ? "1" : "0");
        line = line.Replace("<ingroup>", ctx.IsInGroup ? "1" : "0");
        line = line.Replace("<groupexists>", ctx.GroupExists ? "1" : "0");
        line = line.Replace("<isbust>", ctx.IsBust ? "1" : "0");
        line = line.Replace("<isstand>", ctx.IsStand ? "1" : "0");
        line = line.Replace("<isblackjack>", ctx.IsBlackjack ? "1" : "0");
        line = line.Replace("<ischarlie>", ctx.IsCharlie ? "1" : "0");
        line = line.Replace("<isdone>", ctx.IsDone ? "1" : "0");
        line = line.Replace("<isdd>", ctx.IsDoubleDown ? "1" : "0");

        return line;
    }

    public static bool TryParseFunctionCall(string line, out string funcName, out string[] args)
    {
        funcName = "";
        args = Array.Empty<string>();

        int parenOpen = line.IndexOf('(');
        if (parenOpen < 0) return false;

        int parenClose = line.LastIndexOf(')');
        if (parenClose <= parenOpen) return false;

        funcName = line.Substring(0, parenOpen).Trim();
        if (string.IsNullOrEmpty(funcName)) return false;

        var argsStr = line.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();
        if (string.IsNullOrEmpty(argsStr))
        {
            args = Array.Empty<string>();
            return true;
        }

        var argList = new List<string>();
        int nestDepth = 0;
        int start = 0;
        bool inQuotes = false;
        for (int i = 0; i < argsStr.Length; i++)
        {
            char c = argsStr[i];
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes)
            {
                if (c == '(') nestDepth++;
                else if (c == ')') nestDepth--;
                else if (c == ',' && nestDepth == 0)
                {
                    argList.Add(argsStr.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
        }
        argList.Add(argsStr.Substring(start).Trim());

        args = argList.ToArray();
        return true;
    }

    private static void ExecuteFunction(string funcName, string[] args,
        DrawLogicContext ctx, List<DrawLogicEntry> allEntries, int depth)
    {
        switch (funcName)
        {
            case "BeginShape" when args.Length >= 3:
                _drawing!.BeginShape(EvalFloat(args[0]), EvalFloat(args[1]), EvalFloat(args[2]));
                break;
            case "SetDrawColor" when args.Length >= 4:
                _drawing!.SetDrawColor(EvalFloat(args[0]), EvalFloat(args[1]), EvalFloat(args[2]), EvalFloat(args[3]));
                break;
            case "SetFillColor" when args.Length >= 4:
                _drawing!.SetFillColor(EvalFloat(args[0]), EvalFloat(args[1]), EvalFloat(args[2]), EvalFloat(args[3]));
                break;
            case "BeginPath":
                _drawing!.BeginPath();
                break;
            case "MoveTo" when args.Length >= 3:
                _drawing!.MoveTo(EvalFloat(args[0]), EvalFloat(args[1]), EvalFloat(args[2]));
                break;
            case "LineTo" when args.Length >= 3:
                _drawing!.LineTo(EvalFloat(args[0]), EvalFloat(args[1]), EvalFloat(args[2]));
                break;
            case "EndPath":
                _drawing!.EndPath();
                break;
            case "ClosePath":
                _drawing!.ClosePath();
                break;
            case "FinishShape":
                _currentShape = _drawing!.FinishShape();
                break;
            case "Draw":
                if (_currentShape != null)
                {
                    _pendingDraws.Add(_currentShape);
                    _currentShape = null;
                }
                _vars.Clear();
                break;
            case "Move" when args.Length >= 3:
                _currentShape?.Move(EvalFloat(args[0]), EvalFloat(args[1]), EvalFloat(args[2]));
                break;
            case "Rotate" when args.Length >= 1:
                _currentShape?.Rotate(EvalFloat(args[0]));
                break;
            case "RotateTowards" when args.Length >= 2:
                _currentShape?.RotateTowards(EvalFloat(args[0]), EvalFloat(args[1]));
                break;
            case "SetLineThickness" when args.Length >= 1:
                _drawing!.DefaultLineThickness = EvalFloat(args[0]);
                break;
            case "DrawChar" when args.Length >= 4:
                ExecuteDrawChar(EvalString(args[0]), EvalFloat(args[1]), EvalFloat(args[2]), EvalFloat(args[3]));
                break;
            case "CallDrawLogic":
                HandleCallDrawLogic(args, ctx, allEntries, depth);
                break;
            case "SetVar" when args.Length >= 2:
                SetVarScoped(EvalString(args[0]), EvalFloat(args[1]), ctx);
                break;
            case "UnVar" when args.Length >= 1:
                UnVarScoped(EvalString(args[0]), ctx);
                break;
            case "setVarH" when args.Length >= 3:
                SetVarHand(EvalString(args[0]), (int)EvalFloat(args[1]), EvalFloat(args[2]), ctx);
                break;
            case "unVarH" when args.Length >= 2:
                UnVarHand(EvalString(args[0]), (int)EvalFloat(args[1]), ctx);
                break;
            case "setVarC" when args.Length >= 4:
                SetVarCard(EvalString(args[0]), (int)EvalFloat(args[1]), (int)EvalFloat(args[2]), EvalFloat(args[3]), ctx);
                break;
            case "unVarC" when args.Length >= 3:
                UnVarCard(EvalString(args[0]), (int)EvalFloat(args[1]), (int)EvalFloat(args[2]), ctx);
                break;
        }
    }

    private static string BuildVarKey(string name, DrawLogicContext ctx)
    {
        string key = $"{name}-{ctx.PlayerName}-{ctx.WorldName}";
        if (ctx.IterCardIndex >= 0)
        {
            int hi = ctx.IterHandIndex >= 0 ? ctx.IterHandIndex : (ctx.SourcePlayer?.CurrentHandIndex ?? 0);
            key += $"-{hi}-{ctx.IterCardIndex}";
        }
        else if (ctx.IterHandIndex >= 0)
        {
            key += $"-{ctx.IterHandIndex}";
        }
        return key;
    }

    private static void SetVarScoped(string name, float value, DrawLogicContext ctx)
    {
        _vars[BuildVarKey(name, ctx)] = value;
    }

    private static void UnVarScoped(string name, DrawLogicContext ctx)
    {
        _vars.Remove(BuildVarKey(name, ctx));
    }

    private static void SetVarHand(string name, int handIdx, float value, DrawLogicContext ctx)
    {
        string key = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{handIdx}";
        _vars[key] = value;
    }

    private static void UnVarHand(string name, int handIdx, DrawLogicContext ctx)
    {
        string key = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{handIdx}";
        _vars.Remove(key);
    }

    private static void SetVarCard(string name, int handIdx, int cardIdx, float value, DrawLogicContext ctx)
    {
        string key = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{handIdx}-{cardIdx}";
        _vars[key] = value;
    }

    private static void UnVarCard(string name, int handIdx, int cardIdx, DrawLogicContext ctx)
    {
        string key = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{handIdx}-{cardIdx}";
        _vars.Remove(key);
    }

    private static float GetVarFallback(string name, DrawLogicContext ctx)
    {
        if (ctx.IterCardIndex >= 0)
        {
            int hi = ctx.IterHandIndex >= 0 ? ctx.IterHandIndex : (ctx.SourcePlayer?.CurrentHandIndex ?? 0);
            string cardKey = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{hi}-{ctx.IterCardIndex}";
            if (_vars.TryGetValue(cardKey, out var cv)) return cv;
        }
        if (ctx.IterHandIndex >= 0)
        {
            string handKey = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{ctx.IterHandIndex}";
            if (_vars.TryGetValue(handKey, out var hv)) return hv;
        }
        string playerKey = $"{name}-{ctx.PlayerName}-{ctx.WorldName}";
        if (_vars.TryGetValue(playerKey, out var pv)) return pv;
        return 0;
    }

    private static float GetVarHandExplicit(string name, int handIdx, DrawLogicContext ctx)
    {
        string key = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{handIdx}";
        return _vars.TryGetValue(key, out var v) ? v : 0;
    }

    private static float GetVarCardExplicit(string name, int handIdx, int cardIdx, DrawLogicContext ctx)
    {
        string key = $"{name}-{ctx.PlayerName}-{ctx.WorldName}-{handIdx}-{cardIdx}";
        return _vars.TryGetValue(key, out var v) ? v : 0;
    }

    private static Vector2 V(float x, float y) => new(x, y);

    private static readonly Dictionary<char, Vector2[][]> CharDefs = new()
    {
        ['0'] = [
            [V( 0.034f, -0.678f), V( 0.222f, -0.678f),
             V( 0.222f, -0.865f), V( 0.034f, -0.865f),
             V(-0.153f, -0.865f), V(-0.341f, -0.865f),
             V(-0.341f, -0.678f), V(-0.153f, -0.678f),
             V( 0.034f, -0.678f)],
            [V( 0.222f, -0.678f), V( 0.222f, -0.490f),
             V( 0.222f, -0.303f), V( 0.222f, -0.115f),
             V( 0.222f,  0.072f), V( 0.222f,  0.260f),
             V( 0.222f,  0.447f), V( 0.222f,  0.635f),
             V( 0.409f,  0.635f), V( 0.409f,  0.447f),
             V( 0.409f,  0.260f), V( 0.409f,  0.072f),
             V( 0.409f, -0.115f), V( 0.409f, -0.303f),
             V( 0.409f, -0.490f), V( 0.409f, -0.678f),
             V( 0.222f, -0.678f)],
            [V(-0.341f, -0.678f), V(-0.528f, -0.678f),
             V(-0.528f, -0.490f), V(-0.528f, -0.303f),
             V(-0.528f, -0.115f), V(-0.528f,  0.072f),
             V(-0.528f,  0.260f), V(-0.528f,  0.447f),
             V(-0.528f,  0.635f), V(-0.341f,  0.635f),
             V(-0.341f,  0.447f), V(-0.341f,  0.260f),
             V(-0.341f,  0.072f), V(-0.341f, -0.115f),
             V(-0.341f, -0.303f), V(-0.341f, -0.490f),
             V(-0.341f, -0.678f)],
            [V( 0.222f,  0.635f), V( 0.034f,  0.635f),
             V(-0.153f,  0.635f), V(-0.341f,  0.635f),
             V(-0.341f,  0.822f), V(-0.153f,  0.822f),
             V( 0.034f,  0.822f), V( 0.222f,  0.822f),
             V( 0.222f,  0.635f)],
        ],
        ['1'] = [
            [V(-0.118f, -0.865f), V( 0.069f, -0.865f),
             V( 0.257f, -0.865f), V( 0.257f, -0.678f),
             V( 0.069f, -0.678f), V( 0.069f, -0.490f),
             V( 0.069f, -0.303f), V( 0.069f, -0.115f),
             V( 0.069f,  0.072f), V( 0.069f,  0.260f),
             V( 0.069f,  0.447f), V( 0.257f,  0.447f),
             V( 0.257f,  0.635f), V( 0.069f,  0.635f),
             V( 0.069f,  0.822f), V(-0.118f,  0.822f),
             V(-0.118f,  0.635f), V(-0.118f,  0.447f),
             V(-0.118f,  0.260f), V(-0.118f,  0.072f),
             V(-0.118f, -0.115f), V(-0.118f, -0.303f),
             V(-0.118f, -0.490f), V(-0.118f, -0.678f),
             V(-0.306f, -0.678f), V(-0.306f, -0.865f),
             V(-0.118f, -0.865f)],
        ],
        ['2'] = [
            [V( 0.478f,  0.447f), V( 0.478f,  0.635f),
             V(0.290f,  0.635f), V(0.290f,  0.447f),
             V( 0.478f,  0.447f)],
            [V(-0.272f, -0.678f), V(-0.460f, -0.678f),
             V(-0.460f, -0.865f), V(-0.272f, -0.865f),
             V(-0.085f, -0.865f), V( 0.103f, -0.865f),
             V( 0.290f, -0.865f), V( 0.478f, -0.865f),
             V( 0.478f, -0.678f), V( 0.478f, -0.490f),
             V( 0.290f, -0.490f), V( 0.290f, -0.678f),
             V( 0.103f, -0.678f), V(-0.085f, -0.678f),
             V(-0.272f, -0.678f)],
            [V( 0.290f, -0.490f), V( 0.290f, -0.303f),
             V( 0.103f, -0.303f), V( 0.103f, -0.490f),
             V( 0.290f, -0.490f)],
            [V( 0.103f, -0.303f), V( 0.103f, -0.115f),
             V(-0.085f, -0.115f), V(-0.085f, -0.303f),
             V( 0.103f, -0.303f)],
            [V(-0.085f, -0.115f), V(-0.085f,  0.072f),
             V(-0.272f,  0.072f), V(-0.272f, -0.115f),
             V(-0.085f, -0.115f)],
            [V(-0.272f,  0.072f), V(-0.272f,  0.260f),
             V(-0.272f,  0.447f), V(-0.272f,  0.635f),
             V(-0.460f,  0.635f), V(-0.460f,  0.447f),
             V(-0.460f,  0.260f), V(-0.460f,  0.072f),
             V(-0.272f,  0.072f)],
            [V(-0.272f,  0.635f), V(-0.085f,  0.635f),
             V( 0.103f,  0.635f), V( 0.290f,  0.635f),
             V( 0.290f,  0.822f), V( 0.103f,  0.822f),
             V(-0.085f,  0.822f), V(-0.272f,  0.822f),
             V(-0.272f,  0.635f)],
        ],
        ['3'] = [
            [V( 0.477f,  0.447f), V( 0.477f,  0.635f),
             V( 0.289f,  0.635f), V( 0.289f,  0.447f),
             V( 0.477f,  0.447f)],
            [V(-0.086f, -0.678f), V(-0.273f, -0.678f),
             V(-0.273f, -0.865f), V(-0.086f, -0.865f),
             V( 0.102f, -0.865f), V( 0.289f, -0.865f),
             V( 0.289f, -0.678f), V( 0.102f, -0.678f),
             V(-0.086f, -0.678f)],
            [V(-0.273f, -0.678f), V(-0.273f, -0.490f),
             V(-0.273f, -0.303f), V(-0.273f, -0.115f),
             V(-0.273f,  0.072f), V(-0.461f,  0.072f),
             V(-0.461f, -0.115f), V(-0.461f, -0.303f),
             V(-0.461f, -0.490f), V(-0.461f, -0.678f),
             V(-0.273f, -0.678f)],
            [V( 0.289f, -0.678f), V( 0.477f, -0.678f),
             V( 0.477f, -0.490f), V( 0.289f, -0.490f),
             V( 0.289f, -0.678f)],
            [V(-0.273f,  0.072f), V(-0.086f,  0.072f),
             V( 0.102f,  0.072f), V( 0.102f,  0.260f),
             V(-0.086f,  0.260f), V(-0.273f,  0.260f),
             V(-0.273f,  0.072f)],
            [V(-0.273f,  0.260f), V(-0.273f,  0.447f),
             V(-0.273f,  0.635f), V(-0.461f,  0.635f),
             V(-0.461f,  0.447f), V(-0.461f,  0.260f),
             V(-0.273f,  0.260f)],
            [V(-0.273f,  0.635f), V(-0.086f,  0.635f),
             V( 0.102f,  0.635f), V( 0.289f,  0.635f),
             V( 0.289f,  0.822f), V( 0.102f,  0.822f),
             V(-0.086f,  0.822f), V(-0.273f,  0.822f),
             V(-0.273f,  0.635f)],
        ],
        ['4'] = [
            [V(-0.180f, -0.865f), V(-0.180f, -0.678f),
             V(-0.180f, -0.490f), V(-0.180f, -0.303f),
             V( 0.008f, -0.303f), V( 0.195f, -0.303f),
             V( 0.383f, -0.303f), V( 0.383f, -0.115f),
             V( 0.383f,  0.072f), V( 0.383f,  0.260f),
             V( 0.383f,  0.447f), V( 0.383f,  0.635f),
             V( 0.195f,  0.635f), V( 0.195f,  0.447f),
             V( 0.195f,  0.260f), V( 0.195f,  0.072f),
             V( 0.195f, -0.115f), V( 0.008f, -0.115f),
             V(-0.180f, -0.115f), V(-0.180f,  0.072f),
             V(-0.180f,  0.260f), V(-0.180f,  0.447f),
             V(-0.180f,  0.635f), V(-0.180f,  0.822f),
             V(-0.367f,  0.822f), V(-0.367f,  0.635f),
             V(-0.367f,  0.447f), V(-0.367f,  0.260f),
             V(-0.367f,  0.072f), V(-0.367f, -0.115f),
             V(-0.367f, -0.303f), V(-0.367f, -0.490f),
             V(-0.367f, -0.678f), V(-0.367f, -0.865f),
             V(-0.180f, -0.865f)],
        ],
        ['5'] = [
            [V(-0.093f, -0.677f), V(-0.280f, -0.677f),
             V(-0.280f, -0.865f), V(-0.093f, -0.865f),
             V( 0.095f, -0.865f), V( 0.282f, -0.865f),
             V( 0.282f, -0.677f), V( 0.095f, -0.677f),
             V(-0.093f, -0.677f)],
            [V(-0.280f, -0.677f), V(-0.280f, -0.490f),
             V(-0.280f, -0.302f), V(-0.280f, -0.115f),
             V(-0.280f,  0.073f), V(-0.468f,  0.073f),
             V(-0.468f, -0.115f), V(-0.468f, -0.302f),
             V(-0.468f, -0.490f), V(-0.468f, -0.677f),
             V(-0.280f, -0.677f)],
            [V( 0.282f, -0.677f), V( 0.470f, -0.677f),
             V( 0.470f, -0.490f), V( 0.282f, -0.490f),
             V( 0.282f, -0.677f)],
            [V(-0.280f,  0.073f), V(-0.093f,  0.073f),
             V( 0.095f,  0.073f), V( 0.282f,  0.073f),
             V( 0.470f,  0.073f), V( 0.470f,  0.260f),
             V( 0.470f,  0.448f), V( 0.470f,  0.635f),
             V( 0.282f,  0.635f), V( 0.282f,  0.448f),
             V( 0.282f,  0.260f), V( 0.095f,  0.260f),
             V(-0.093f,  0.260f), V(-0.280f,  0.260f),
             V(-0.280f,  0.073f)],
            [V(-0.280f,  0.823f), V(-0.468f,  0.823f),
             V(-0.468f,  0.635f), V(-0.280f,  0.635f),
             V(-0.093f,  0.635f), V( 0.095f,  0.635f),
             V( 0.282f,  0.635f), V( 0.282f,  0.823f),
             V( 0.095f,  0.823f), V(-0.093f,  0.823f),
             V(-0.280f,  0.823f)],
        ],
        ['6'] = [
            [V(-0.314f,  0.448f), V(-0.314f,  0.635f),
             V(-0.501f,  0.635f), V(-0.501f,  0.448f),
             V(-0.314f,  0.448f)],
            [V(-0.126f, -0.677f), V(-0.314f, -0.677f),
             V(-0.314f, -0.865f), V(-0.126f, -0.865f),
             V( 0.061f, -0.865f), V( 0.249f, -0.865f),
             V( 0.249f, -0.677f), V( 0.061f, -0.677f),
             V(-0.126f, -0.677f)],
            [V(-0.314f, -0.677f), V(-0.314f, -0.490f),
             V(-0.314f, -0.302f), V(-0.314f, -0.115f),
             V(-0.314f,  0.073f), V(-0.501f,  0.073f),
             V(-0.501f, -0.115f), V(-0.501f, -0.302f),
             V(-0.501f, -0.490f), V(-0.501f, -0.677f),
             V(-0.314f, -0.677f)],
            [V( 0.249f, -0.677f), V( 0.436f, -0.677f),
             V( 0.436f, -0.490f), V( 0.436f, -0.302f),
             V( 0.436f, -0.115f), V( 0.436f,  0.073f),
             V( 0.436f,  0.260f), V( 0.436f,  0.448f),
             V( 0.436f,  0.635f), V( 0.249f,  0.635f),
             V( 0.249f,  0.448f), V( 0.249f,  0.260f),
             V( 0.061f,  0.260f), V(-0.126f,  0.260f),
             V(-0.314f,  0.260f), V(-0.314f,  0.073f),
             V(-0.126f,  0.073f), V( 0.061f,  0.073f),
             V( 0.249f,  0.073f), V( 0.249f, -0.115f),
             V( 0.249f, -0.302f), V( 0.249f, -0.490f),
             V( 0.249f, -0.677f)],
            [V(-0.314f,  0.635f), V(-0.126f,  0.635f),
             V( 0.061f,  0.635f), V( 0.249f,  0.635f),
             V( 0.249f,  0.823f), V( 0.061f,  0.823f),
             V(-0.126f,  0.823f), V(-0.314f,  0.823f),
             V(-0.314f,  0.635f)],
        ],
        ['7'] = [
            [V( 0.180f, -0.865f), V( 0.180f, -0.678f),
             V( 0.180f, -0.490f), V( 0.180f, -0.303f),
             V( 0.180f, -0.115f), V(-0.008f, -0.115f),
             V(-0.008f, -0.303f), V(-0.008f, -0.490f),
             V(-0.008f, -0.678f), V(-0.008f, -0.865f),
             V( 0.180f, -0.865f)],
            [V(-0.008f, -0.115f), V(-0.008f,  0.072f),
             V(-0.195f,  0.072f), V(-0.195f, -0.115f),
             V(-0.008f, -0.115f)],
            [V(-0.195f,  0.072f), V(-0.195f,  0.260f),
             V(-0.195f,  0.447f), V(-0.195f,  0.635f),
             V(-0.383f,  0.635f), V(-0.383f,  0.447f),
             V(-0.383f,  0.260f), V(-0.383f,  0.072f),
             V(-0.195f,  0.072f)],
            [V(-0.195f,  0.635f), V(-0.008f,  0.635f),
             V( 0.180f,  0.635f), V( 0.367f,  0.635f),
             V( 0.555f,  0.635f), V( 0.555f,  0.822f),
             V( 0.367f,  0.822f), V( 0.180f,  0.822f),
             V(-0.008f,  0.822f), V(-0.195f,  0.822f),
             V(-0.195f,  0.635f)],
        ],
        ['8'] = [
            [V( 0.075f, -0.677f), V( 0.262f, -0.677f),
             V( 0.262f, -0.865f), V( 0.075f, -0.865f),
             V(-0.113f, -0.865f), V(-0.300f, -0.865f),
             V(-0.300f, -0.677f), V(-0.113f, -0.677f),
             V( 0.075f, -0.677f)],
            [V( 0.262f, -0.677f), V( 0.262f, -0.490f),
             V( 0.262f, -0.302f), V( 0.262f, -0.115f),
             V( 0.450f, -0.115f), V( 0.450f, -0.302f),
             V( 0.450f, -0.490f), V( 0.450f, -0.677f),
             V( 0.262f, -0.677f)],
            [V(-0.300f, -0.677f), V(-0.488f, -0.677f),
             V(-0.488f, -0.490f), V(-0.488f, -0.302f),
             V(-0.488f, -0.115f), V(-0.300f, -0.115f),
             V(-0.300f, -0.302f), V(-0.300f, -0.490f),
             V(-0.300f, -0.677f)],
            [V( 0.262f, -0.115f), V(0.075f, -0.115f),
             V(-0.113f, -0.115f), V(-0.300f, -0.115f),
             V(-0.300f,  0.073f), V(-0.113f,  0.073f),
             V( 0.075f,  0.073f), V( 0.262f,  0.073f),
             V( 0.262f, -0.115f)],
            [V( 0.262f,  0.073f), V( 0.262f,  0.260f),
             V( 0.262f,  0.448f), V( 0.262f,  0.635f),
             V( 0.450f,  0.635f), V( 0.450f,  0.448f),
             V( 0.450f,  0.260f), V( 0.450f,  0.073f),
             V( 0.262f,  0.073f)],
            [V(-0.300f,  0.073f), V(-0.488f,  0.073f),
             V(-0.488f,  0.260f), V(-0.488f,  0.448f),
             V(-0.488f,  0.635f), V(-0.300f,  0.635f),
             V(-0.300f,  0.448f), V(-0.300f,  0.260f),
             V(-0.300f,  0.073f)],
            [V( 0.262f,  0.635f), V( 0.075f,  0.635f),
             V(-0.113f,  0.635f), V(-0.300f,  0.635f),
             V(-0.300f,  0.823f), V(-0.113f,  0.823f),
             V( 0.075f,  0.823f), V( 0.262f,  0.823f),
             V( 0.262f,  0.635f)],
        ],
        ['9'] = [
            [V(-0.099f, -0.678f), V(-0.287f, -0.678f),
             V(-0.287f, -0.865f), V(-0.099f, -0.865f),
             V( 0.088f, -0.865f), V( 0.276f, -0.865f),
             V( 0.276f, -0.678f), V( 0.088f, -0.678f),
             V(-0.099f, -0.678f)],
            [V(-0.287f, -0.678f), V(-0.287f, -0.490f),
             V(-0.287f, -0.303f), V(-0.287f, -0.115f),
             V(-0.099f, -0.115f), V( 0.088f, -0.115f),
             V( 0.276f, -0.115f), V( 0.276f,  0.072f),
             V( 0.088f,  0.072f), V(-0.099f,  0.072f),
             V(-0.287f,  0.072f), V(-0.287f,  0.260f),
             V(-0.287f,  0.447f), V(-0.287f,  0.635f),
             V(-0.474f,  0.635f), V(-0.474f,  0.447f),
             V(-0.474f,  0.260f), V(-0.474f,  0.072f),
             V(-0.474f, -0.115f), V(-0.474f, -0.303f),
             V(-0.474f, -0.490f), V(-0.474f, -0.678f),
             V(-0.287f, -0.678f)],
            [V( 0.276f, -0.678f), V( 0.463f, -0.678f),
             V( 0.463f, -0.490f), V( 0.276f, -0.490f),
             V( 0.276f, -0.678f)],
            [V( 0.276f,  0.072f), V( 0.463f,  0.072f),
             V( 0.463f,  0.260f), V( 0.463f,  0.447f),
             V( 0.463f,  0.635f), V( 0.276f,  0.635f),
             V( 0.276f,  0.447f), V( 0.276f,  0.260f),
             V( 0.276f,  0.072f)],
            [V(-0.287f,  0.635f), V(-0.099f,  0.635f),
             V( 0.088f,  0.635f), V( 0.276f,  0.635f),
             V( 0.276f,  0.822f), V( 0.088f,  0.822f),
             V(-0.099f,  0.822f), V(-0.287f,  0.822f),
             V(-0.287f,  0.635f)],
        ],
        ['A'] = [
            [V(-0.394f, -0.865f), V(-0.394f, -0.677f),
             V(-0.394f, -0.490f), V(-0.394f, -0.302f),
             V(-0.394f, -0.115f), V(-0.206f, -0.115f),
             V(-0.019f, -0.115f), V( 0.169f, -0.115f),
             V( 0.356f, -0.115f), V( 0.356f, -0.302f),
             V( 0.356f, -0.490f), V( 0.356f, -0.677f),
             V( 0.356f, -0.865f), V( 0.544f, -0.865f),
             V( 0.544f, -0.677f), V( 0.544f, -0.490f),
             V( 0.544f, -0.302f), V( 0.544f, -0.115f),
             V( 0.544f,  0.073f), V( 0.544f,  0.260f),
             V( 0.544f,  0.448f), V( 0.356f,  0.448f),
             V( 0.356f,  0.260f), V( 0.356f,  0.073f),
             V( 0.169f,  0.073f), V(-0.019f,  0.073f),
             V(-0.206f,  0.073f), V(-0.394f,  0.073f),
             V(-0.394f,  0.260f), V(-0.394f,  0.448f),
             V(-0.581f,  0.448f), V(-0.581f,  0.260f),
             V(-0.581f,  0.073f), V(-0.581f, -0.115f),
             V(-0.581f, -0.302f), V(-0.581f, -0.490f),
             V(-0.581f, -0.677f), V(-0.581f, -0.865f),
             V(-0.394f, -0.865f)],
            [V(-0.394f,  0.448f), V(-0.206f,  0.448f),
             V(-0.206f,  0.635f), V(-0.394f,  0.635f),
             V(-0.394f,  0.448f)],
            [V( 0.356f,  0.448f), V( 0.356f,  0.635f),
             V( 0.169f,  0.635f), V( 0.169f,  0.448f),
             V( 0.356f,  0.448f)],
            [V(-0.206f,  0.635f), V(-0.019f,  0.635f),
             V( 0.169f,  0.635f), V( 0.169f,  0.823f),
             V(-0.019f,  0.823f), V(-0.206f,  0.823f),
             V(-0.206f,  0.635f)],
        ],
        ['J'] = [
            [V( 0.061f, -0.678f), V(-0.126f, -0.678f),
             V(-0.126f, -0.865f), V( 0.061f, -0.865f),
             V( 0.249f, -0.865f), V( 0.249f, -0.678f),
             V( 0.061f, -0.678f)],
            [V(-0.126f, -0.678f), V(-0.126f, -0.490f),
             V(-0.126f, -0.303f), V(-0.126f, -0.115f),
             V(-0.126f,  0.072f), V(-0.126f,  0.260f),
             V(-0.126f,  0.447f), V(-0.126f,  0.635f),
             V( 0.061f,  0.635f), V( 0.061f,  0.822f),
             V(-0.126f,  0.822f), V(-0.314f,  0.822f),
             V(-0.501f,  0.822f), V(-0.501f,  0.635f),
             V(-0.314f,  0.635f), V(-0.314f,  0.447f),
             V(-0.314f,  0.260f), V(-0.314f,  0.072f),
             V(-0.314f, -0.115f), V(-0.314f, -0.303f),
             V(-0.314f, -0.490f), V(-0.314f, -0.678f),
             V(-0.126f, -0.678f)],
            [V( 0.249f, -0.678f), V( 0.436f, -0.678f),
             V( 0.436f, -0.490f), V( 0.436f, -0.303f),
             V( 0.249f, -0.303f), V( 0.249f, -0.490f),
             V( 0.249f, -0.678f)],
        ],
        ['Q'] = [
            [V(-0.701f, -0.677f), V(-0.889f, -0.677f),
             V(-0.889f, -0.865f), V(-0.701f, -0.865f),
             V(-0.514f, -0.865f), V(-0.514f, -0.677f),
             V(-0.701f, -0.677f)],
            [V(-0.139f, -0.677f), V(-0.326f, -0.677f),
             V(-0.326f, -0.865f), V(-0.139f, -0.865f),
             V( 0.049f, -0.865f), V( 0.236f, -0.865f),
             V( 0.424f, -0.865f), V( 0.424f, -0.677f),
             V( 0.236f, -0.677f), V( 0.049f, -0.677f),
             V(-0.139f, -0.677f)],
            [V(-0.514f, -0.677f), V(-0.326f, -0.677f),
             V(-0.326f, -0.490f), V(-0.514f, -0.490f),
             V(-0.514f, -0.677f)],
            [V( 0.424f, -0.677f), V( 0.611f, -0.677f),
             V( 0.611f, -0.490f), V( 0.424f, -0.490f),
             V( 0.424f, -0.677f)],
            [V(-0.514f, -0.490f), V(-0.514f, -0.302f),
             V(-0.514f, -0.115f), V(-0.514f,  0.073f),
             V(-0.514f,  0.260f), V(-0.514f,  0.448f),
             V(-0.701f,  0.448f), V(-0.701f,  0.260f),
             V(-0.701f,  0.073f), V(-0.701f, -0.115f),
             V(-0.701f, -0.302f), V(-0.701f, -0.490f),
             V(-0.514f, -0.490f)],
            [V(-0.326f, -0.490f), V(-0.139f, -0.490f),
             V(-0.139f, -0.302f), V(-0.326f, -0.302f),
             V(-0.326f, -0.490f)],
            [V( 0.611f, -0.490f), V( 0.799f, -0.490f),
             V(0.799f, -0.302f), V( 0.799f, -0.115f),
             V( 0.799f,  0.073f), V( 0.799f,  0.260f),
             V( 0.799f,  0.448f), V( 0.611f,  0.448f),
             V( 0.611f,  0.260f), V( 0.611f,  0.073f),
             V( 0.611f, -0.115f), V( 0.611f, -0.302f),
             V( 0.611f, -0.490f)],
            [V(-0.514f,  0.448f), V(-0.326f,  0.448f),
             V(-0.326f,  0.635f), V(-0.514f,  0.635f),
             V(-0.514f,  0.448f)],
            [V( 0.611f,  0.448f), V( 0.611f,  0.635f),
             V( 0.424f,  0.635f), V( 0.424f,  0.448f),
             V( 0.611f,  0.448f)],
            [V(-0.326f,  0.635f), V(-0.139f,  0.635f),
             V( 0.049f,  0.635f), V( 0.236f,  0.635f),
             V( 0.424f,  0.635f), V( 0.424f,  0.823f),
             V( 0.236f,  0.823f), V( 0.049f,  0.823f),
             V(-0.139f,  0.823f), V(-0.326f,  0.823f),
             V(-0.326f,  0.635f)],
        ],
        ['K'] = [
            [V(-0.501f, -0.865f), V(-0.501f, -0.678f),
             V(-0.501f, -0.490f), V(-0.688f, -0.490f),
             V(-0.688f, -0.678f), V(-0.688f, -0.865f),
             V(-0.501f, -0.865f)],
            [V( 0.624f, -0.865f), V( 0.624f, -0.678f),
             V( 0.624f, -0.490f), V( 0.624f, -0.303f),
             V( 0.624f, -0.115f), V( 0.624f,  0.072f),
             V( 0.624f,  0.260f), V( 0.624f,  0.447f),
             V( 0.624f,  0.635f), V( 0.624f,  0.822f),
             V( 0.437f,  0.822f), V( 0.437f,  0.635f),
             V( 0.437f,  0.447f), V( 0.437f,  0.260f),
             V( 0.437f,  0.072f), V( 0.437f, -0.115f),
             V( 0.437f, -0.303f), V( 0.437f, -0.490f),
             V( 0.437f, -0.678f), V( 0.437f, -0.865f),
             V( 0.624f, -0.865f)],
            [V(-0.501f, -0.490f), V(-0.313f, -0.490f),
             V(-0.313f, -0.303f), V(-0.501f, -0.303f),
             V(-0.501f, -0.490f)],
            [V(-0.313f, -0.303f), V(-0.126f, -0.303f),
             V(-0.126f, -0.115f), V(-0.313f, -0.115f),
             V(-0.313f, -0.303f)],
            [V(-0.126f, -0.115f), V( 0.062f, -0.115f),
             V( 0.249f, -0.115f), V( 0.249f,  0.072f),
             V( 0.062f,  0.072f), V(-0.126f,  0.072f),
             V(-0.126f, -0.115f)],
            [V(-0.126f,  0.072f), V(-0.126f,  0.260f),
             V(-0.313f,  0.260f), V(-0.313f,  0.072f),
             V(-0.126f,  0.072f)],
            [V(-0.313f,  0.260f), V(-0.313f,  0.447f),
             V(-0.501f,  0.447f), V(-0.501f,  0.260f),
             V(-0.313f,  0.260f)],
            [V(-0.501f,  0.447f), V(-0.501f,  0.635f),
             V(-0.501f,  0.822f), V(-0.688f,  0.822f),
             V(-0.688f,  0.635f), V(-0.688f,  0.447f),
             V(-0.501f,  0.447f)],
        ],
    };

    private static readonly Dictionary<char, float> CharXCorrection = new()
    {
        ['0'] = 0.1f,
        ['1'] = 0.15f,
    };

    private static void ExecuteDrawChar(string letter, float offsetX, float offsetY, float scale)
    {
        if (_drawing == null || string.IsNullOrEmpty(letter)) return;
        if (!CharDefs.TryGetValue(letter[0], out var paths)) return;

        float xCorrection = CharXCorrection.GetValueOrDefault(letter[0], 0f);

        foreach (var path in paths)
        {
            _drawing.BeginPath();
            for (int i = 0; i < path.Length; i++)
            {
                float px = (path[i].X + xCorrection) * scale + offsetX;
                float py = path[i].Y * scale + offsetY;
                if (i == 0)
                    _drawing.MoveTo(px, py, 0);
                else
                    _drawing.LineTo(px, py, 0);
            }
            _drawing.EndPath();
        }
    }

    private static void HandleCallDrawLogic(string[] args, DrawLogicContext ctx,
        List<DrawLogicEntry> allEntries, int depth)
    {
        if (args.Length < 1) return;
        var targetName = EvalString(args[0]);
        var target = allEntries.FirstOrDefault(e => e.Name == targetName);
        if (target == null) return;
        if (!target.IsActive) return;

        if (target.IsIterate && _execPlayers != null && _execDealer != null && _execConfig != null)
        {
            RunIterated(target.Script, allEntries, _execPlayers, _execDealer, _execConfig, depth + 1);
        }
        else if (args.Length >= 4)
        {
            var ctxCopy = CloneContext(ctx);
            ctxCopy.Position = new Vector3(EvalFloat(args[1]), EvalFloat(args[2]), EvalFloat(args[3]));
            Execute(target.Script, ctxCopy, allEntries, depth + 1);
        }
        else
        {
            Execute(target.Script, ctx, allEntries, depth + 1);
        }
    }

    private static DrawLogicContext CloneContext(DrawLogicContext src)
    {
        return new DrawLogicContext
        {
            Position = src.Position,
            PlayerName = src.PlayerName,
            IsDealer = src.IsDealer,
            Score = src.Score,
            Cards = src.Cards,
            CardCount = src.CardCount,
            Bank = src.Bank,
            Bet = src.Bet,
            HandIndex = src.HandIndex,
            HandCount = src.HandCount,
            Rotation = src.Rotation,
            IsFocused = src.IsFocused,
            IsNearby = src.IsNearby,
            IsVisible = src.IsVisible,
            IsOnline = src.IsOnline,
            IsInGroup = src.IsInGroup,
            GroupExists = src.GroupExists,
            IsBust = src.IsBust,
            IsStand = src.IsStand,
            IsBlackjack = src.IsBlackjack,
            IsCharlie = src.IsCharlie,
            IsDone = src.IsDone,
            IsDoubleDown = src.IsDoubleDown,
            SourcePlayer = src.SourcePlayer,
            Config = src.Config,
            WorldName = src.WorldName,
            IsPlaying = src.IsPlaying,
            MaxBet = src.MaxBet,
            IterHandIndex = src.IterHandIndex,
            IterHandCount = src.IterHandCount,
            IterHandPoints = src.IterHandPoints,
            IterHandPointsB = src.IterHandPointsB,
            IterHandBusted = src.IterHandBusted,
            IterHandActive = src.IterHandActive,
            IterCardIndex = src.IterCardIndex,
            IterCardCount = src.IterCardCount,
            IterCardNumber = src.IterCardNumber,
            IterCardColor = src.IterCardColor,
            IterCardDrawnAt = src.IterCardDrawnAt,
            IterLetterIndex = src.IterLetterIndex,
            IterLetterCount = src.IterLetterCount,
            IterLetter = src.IterLetter,
            DealerDirection = src.DealerDirection,
            CameraDirection = src.CameraDirection,
        };
    }

    public static string EvalString(string arg)
    {
        arg = arg.Trim();
        if (arg.Length >= 2 && arg[0] == '"' && arg[^1] == '"')
            return arg.Substring(1, arg.Length - 2);
        return arg;
    }

    public static float EvalFloat(string expr)
    {
        expr = expr.Trim();
        int pos = 0;
        return ParseAddSub(expr, ref pos);
    }

    private static float ParseAddSub(string expr, ref int pos)
    {
        var left = ParseMulDiv(expr, ref pos);
        while (pos < expr.Length)
        {
            SkipSpace(expr, ref pos);
            if (pos >= expr.Length) break;
            char op = expr[pos];
            if (op != '+' && op != '-') break;
            pos++;
            var right = ParseMulDiv(expr, ref pos);
            left = op == '+' ? left + right : left - right;
        }
        return left;
    }

    private static float ParseMulDiv(string expr, ref int pos)
    {
        var left = ParseUnary(expr, ref pos);
        while (pos < expr.Length)
        {
            SkipSpace(expr, ref pos);
            if (pos >= expr.Length) break;
            char op = expr[pos];
            if (op != '*' && op != '/' && op != '%') break;
            pos++;
            var right = ParseUnary(expr, ref pos);
            if (op == '*') left *= right;
            else if (op == '/') left = right != 0 ? left / right : 0;
            else left = right != 0 ? left % right : 0;
        }
        return left;
    }

    private static float ParseUnary(string expr, ref int pos)
    {
        SkipSpace(expr, ref pos);
        if (pos < expr.Length && expr[pos] == '-')
        {
            pos++;
            return -ParsePrimary(expr, ref pos);
        }
        return ParsePrimary(expr, ref pos);
    }

    private static float ParsePrimary(string expr, ref int pos)
    {
        SkipSpace(expr, ref pos);
        if (pos < expr.Length && expr[pos] == '(')
        {
            pos++;
            var val = ParseAddSub(expr, ref pos);
            SkipSpace(expr, ref pos);
            if (pos < expr.Length && expr[pos] == ')') pos++;
            return val;
        }

        if (pos < expr.Length && char.IsLetter(expr[pos]))
        {
            int idStart = pos;
            while (pos < expr.Length && (char.IsLetterOrDigit(expr[pos]) || expr[pos] == '_'))
                pos++;
            string ident = expr.Substring(idStart, pos - idStart);

            SkipSpace(expr, ref pos);
            if (pos < expr.Length && expr[pos] == '(')
            {
                pos++;
                var funcArgs = new List<float>();
                var stringArgs = new List<string>();
                bool isVarFunc = ident == "GetVar" || ident == "getVarH" || ident == "getVarC";

                if (isVarFunc)
                {
                    SkipSpace(expr, ref pos);
                    if (pos < expr.Length && expr[pos] == '"')
                    {
                        pos++;
                        int qStart = pos;
                        while (pos < expr.Length && expr[pos] != '"') pos++;
                        stringArgs.Add(expr.Substring(qStart, pos - qStart));
                        if (pos < expr.Length && expr[pos] == '"') pos++;
                        SkipSpace(expr, ref pos);
                        while (pos < expr.Length && expr[pos] == ',')
                        {
                            pos++;
                            funcArgs.Add(ParseAddSub(expr, ref pos));
                            SkipSpace(expr, ref pos);
                        }
                    }
                }
                else
                {
                    SkipSpace(expr, ref pos);
                    if (pos < expr.Length && expr[pos] != ')')
                    {
                        funcArgs.Add(ParseAddSub(expr, ref pos));
                        SkipSpace(expr, ref pos);
                        while (pos < expr.Length && expr[pos] == ',')
                        {
                            pos++;
                            funcArgs.Add(ParseAddSub(expr, ref pos));
                            SkipSpace(expr, ref pos);
                        }
                    }
                }

                SkipSpace(expr, ref pos);
                if (pos < expr.Length && expr[pos] == ')') pos++;

                return EvalMathFunc(ident, funcArgs, stringArgs);
            }

            return 0;
        }

        int start = pos;
        while (pos < expr.Length && (char.IsDigit(expr[pos]) || expr[pos] == '.'))
            pos++;

        if (start == pos) return 0;
        return float.TryParse(expr.AsSpan(start, pos - start),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    private static float EvalMathFunc(string name, List<float> args, List<string> stringArgs)
    {
        switch (name)
        {
            case "Ceil" when args.Count >= 1:
                return (float)Math.Ceiling(args[0]);
            case "Floor" when args.Count >= 1:
                return (float)Math.Floor(args[0]);
            case "Sin" when args.Count >= 1:
                return (float)Math.Sin(args[0]);
            case "Cos" when args.Count >= 1:
                return (float)Math.Cos(args[0]);
            case "Sqrt" when args.Count >= 1:
                return (float)Math.Sqrt(args[0]);
            case "Min" when args.Count >= 2:
                return Math.Min(args[0], args[1]);
            case "Max" when args.Count >= 2:
                return Math.Max(args[0], args[1]);
            case "Mul" when args.Count >= 2:
                return args[0] * args[1];
            case "Div" when args.Count >= 2:
                return args[1] != 0 ? args[0] / args[1] : 0;
            case "Mod" when args.Count >= 2:
                return args[1] != 0 ? args[0] % args[1] : 0;
            case "Plus" when args.Count >= 2:
                return args[0] + args[1];
            case "Minus" when args.Count >= 2:
                return args[0] - args[1];
            case "Clamp" when args.Count >= 3:
                return Math.Clamp(args[0], args[1], args[2]);
            case "AlterRot" when args.Count >= 2:
                return args[0] + args[1] * (MathF.PI / 180f);

            case "GetVar" when stringArgs.Count >= 1:
                return _evalCtx != null ? GetVarFallback(stringArgs[0], _evalCtx) : 0;
            case "getVarH" when stringArgs.Count >= 1 && args.Count >= 1:
                return _evalCtx != null ? GetVarHandExplicit(stringArgs[0], (int)args[0], _evalCtx) : 0;
            case "getVarC" when stringArgs.Count >= 1 && args.Count >= 2:
                return _evalCtx != null ? GetVarCardExplicit(stringArgs[0], (int)args[0], (int)args[1], _evalCtx) : 0;

            default:
                return 0;
        }
    }

    private static void SkipSpace(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
    }

    private static bool EvaluateIfCondition(string resolvedLine)
    {
        var trimmed = resolvedLine.Trim();
        if (!trimmed.StartsWith("if ")) return false;

        var inner = trimmed.Substring(3);
        if (inner.EndsWith("{"))
            inner = inner.Substring(0, inner.Length - 1);

        var eqIdx = inner.IndexOf('=');
        if (eqIdx < 0) return false;

        var left = inner.Substring(0, eqIdx).Trim();
        var right = inner.Substring(eqIdx + 1).Trim();

        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static Vector3 GetDealerPosition()
    {
        if (DrawLogicDebugManager.IsActive) return Vector3.Zero;
        var local = Plugin.ObjectTable.LocalPlayer;
        return local?.Position ?? Vector3.Zero;
    }

    private static unsafe float GetCameraDirection()
    {
        var cm = CameraManager.Instance();
        if (cm == null) return 0f;
        var cam = cm->GetActiveCamera();
        if (cam == null) return 0f;
        return cam->DirH + MathF.PI;
    }

    private static unsafe Vector3 GetCameraPosition()
    {
        var cm = CameraManager.Instance();
        if (cm == null) return Vector3.Zero;
        var cam = cm->GetActiveCamera();
        if (cam == null) return Vector3.Zero;
        var p = cam->SceneCamera.Position;
        return new Vector3(p.X, p.Y, p.Z);
    }

    private static float ComputeDirection(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        if (dx == 0f && dz == 0f) return 0f;
        return MathF.Atan2(dx, dz);
    }
}
