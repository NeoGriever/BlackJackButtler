using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

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
    private static DrawLogicContext? _evalCtx;

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
        }
        finally
        {
            _execPlayers = null;
            _execDealer = null;
            _execConfig = null;
        }
    }

    private static void RunIterated(string script, List<DrawLogicEntry> entries,
        List<PlayerState> players, PlayerState dealer, Configuration config, int depth)
    {
        var active = players.Where(p => p.IsActivePlayer && !p.IsOnHold && !p.IsOnBench).ToList();
        foreach (var player in active)
        {
            var ctx = BuildContext(player, false, config);
            Execute(script, ctx, entries, depth);
        }
        var dealerCtx = BuildContext(dealer, true, config);
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
                if (obj.ObjectKind != ObjectKind.Player) continue;
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

        var nearby = NearbyPlayersManager.GetNearbyPlayers(config);
        ctx.IsNearby = nearby.Any(n => n.Name == player.Name && n.Distance <= config.NearbyDistanceCap);

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
            line = ReplaceTokens(line, ctx);

            if (TryParseFunctionCall(line, out var funcName, out var args))
                ExecuteFunction(funcName, args, ctx, allEntries, depth);

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
                _currentShape?.Draw();
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
}
