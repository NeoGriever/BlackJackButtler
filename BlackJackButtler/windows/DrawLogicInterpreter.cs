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
}

public static class DrawLogicInterpreter
{
    private const int MaxDepth = 10;

    private static WorldDrawing? _drawing;
    private static Shape? _currentShape;
    private static List<PlayerState>? _execPlayers;
    private static PlayerState? _execDealer;
    private static Configuration? _execConfig;

    public static void ExecuteStartEntry(List<DrawLogicEntry> entries, string startName,
        List<PlayerState> players, PlayerState dealer, Configuration config)
    {
        if (string.IsNullOrEmpty(startName)) return;
        var startEntry = entries.FirstOrDefault(e => e.Name == startName);
        if (startEntry == null) return;

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
                var ctx = BuildContextNoPlayer();
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

    private static DrawLogicContext BuildContextNoPlayer()
    {
        var ctx = new DrawLogicContext
        {
            GroupExists = Plugin.PartyList.Length > 0,
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
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

            line = ReplaceTokens(line, ctx);

            if (TryParseFunctionCall(line, out var funcName, out var args))
                ExecuteFunction(funcName, args, ctx, allEntries, depth);
        }
    }

    public static string ReplaceTokens(string line, DrawLogicContext ctx)
    {
        line = line.Replace("<pos>.x", ctx.Position.X.ToString("F4", CultureInfo.InvariantCulture));
        line = line.Replace("<pos>.y", ctx.Position.Y.ToString("F4", CultureInfo.InvariantCulture));
        line = line.Replace("<pos>.z", ctx.Position.Z.ToString("F4", CultureInfo.InvariantCulture));

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
        }
    }

    private static void HandleCallDrawLogic(string[] args, DrawLogicContext ctx,
        List<DrawLogicEntry> allEntries, int depth)
    {
        if (args.Length < 1) return;
        var targetName = EvalString(args[0]);
        var target = allEntries.FirstOrDefault(e => e.Name == targetName);
        if (target == null) return;

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
            if (op != '*' && op != '/') break;
            pos++;
            var right = ParseUnary(expr, ref pos);
            left = op == '*' ? left * right : (right != 0 ? left / right : 0);
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

        int start = pos;
        while (pos < expr.Length && (char.IsDigit(expr[pos]) || expr[pos] == '.'))
            pos++;

        if (start == pos) return 0;
        return float.TryParse(expr.AsSpan(start, pos - start),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    private static void SkipSpace(string s, ref int pos)
    {
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
    }
}
