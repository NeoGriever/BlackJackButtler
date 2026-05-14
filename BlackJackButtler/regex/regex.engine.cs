using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RRX = System.Text.RegularExpressions;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Regex;

public static class RegexEngine
{
    private static readonly HashSet<string> _nextRoundVotes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(string pattern, bool caseSensitive), RRX.Regex> _regexCache = new();

    private const char IconLetterStart = '\uE071';
    private const char IconLetterEnd = '\uE08A';

    public static void InvalidateCache() => _regexCache.Clear();

    public static void ClearNextRoundVotes() => _nextRoundVotes.Clear();

    public static bool HasPlayerVoted(string name) => _nextRoundVotes.Contains(name);

    public static void CheckAutoReadyStart(List<PlayerState> players, Configuration cfg)
    {
        var phase = GameEngine.CurrentPhase;
        if (phase != GamePhase.Waiting && phase != GamePhase.Payout) return;

        var activePlayers = players.Where(pl => pl.IsActivePlayer && !pl.IsOnHold).ToList();
        if (activePlayers.Count < 2) return;
        if (!activePlayers.All(pl => pl.ReadySkip || _nextRoundVotes.Contains(pl.Name))) return;

        _nextRoundVotes.Clear();
        if (cfg.EnableAutomation && cfg.ShowAutoRunButton && cfg.AutoRun)
            Task.Run(() => GameEngine.StartInitialDeal(players, cfg));
        else
            Plugin.Instance.GetMainWindow().SetHighlightNewRound();
    }

    public static int? LastDetectedCardValue { get; private set; }

    public static bool TryConsumeDetectedCard(out int cardValue)
    {
        if (LastDetectedCardValue.HasValue)
        {
            cardValue = LastDetectedCardValue.Value;
            LastDetectedCardValue = null;
            return true;
        }
        cardValue = 0;
        return false;
    }

    public static int? MapRollToCard(int rolled) => MapValue(rolled);

    public static void ProcessIncoming(ParsedChatMessage msg, Configuration cfg, List<PlayerState> players, PlayerState dealer)
    {
        var cleanMessage = SanitizeForRegex(msg.Message);

        foreach (var entry in cfg.UserRegexes)
        {
            if (!entry.Enabled || entry.Patterns == null || entry.Patterns.Count == 0) continue;

            foreach (var pattern in entry.Patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                var sanitizedPattern = SanitizePatternForRegex(pattern);
                var key = (sanitizedPattern, entry.CaseSensitive);
                if (!_regexCache.TryGetValue(key, out var rx))
                {
                    var options = entry.CaseSensitive
                        ? RRX.RegexOptions.Compiled
                        : (RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);
                    try { rx = new RRX.Regex(sanitizedPattern, options); }
                    catch { continue; }
                    _regexCache[key] = rx;
                }

                if (rx.IsMatch(cleanMessage))
                {
                    if (entry.Mode == RegexEntryMode.Trigger)
                    {
                        ExecuteAction(entry, sanitizedPattern, msg, cleanMessage, players, dealer, cfg);
                    }
                    else if (entry.Mode == RegexEntryMode.SetVariable)
                    {
                        VariableManager.SetVariable(entry.Name, cleanMessage);
                    }
                    break;
                }
            }
        }
    }

    private static void ExecuteAction(UserRegexEntry entry, string matchedPattern, ParsedChatMessage msg, string cleanMessage, List<PlayerState> players, PlayerState dealer, Configuration cfg)
    {
        var p = players.FirstOrDefault(x => x.Name.Equals(msg.Name, StringComparison.OrdinalIgnoreCase));

        var options = entry.CaseSensitive ? RRX.RegexOptions.None : RRX.RegexOptions.IgnoreCase;
        var match = RRX.Regex.Match(cleanMessage, matchedPattern, options);

        switch (entry.Action)
        {
            case RegexAction.DiceRollValue:
                var window = Plugin.Instance.GetMainWindow();
                if (!window.IsRecognitionActive && !Plugin.IsDebugMode)
                    break;
                if (!CommandExecutor.IsRunning)
                    break;
                if (match.Success && match.Groups.Count >= 2)
                {
                    if (int.TryParse(match.Groups[1].Value, out var rolled))
                    {
                        var card = MapValue(rolled);
                        if (card.HasValue)
                        {
                            LastDetectedCardValue = card.Value;
                            DiceResultHandler.HandleDiceResult(card.Value, cfg, players, dealer);
                        }
                    }
                }
                break;

            case RegexAction.TradePartner:
                if (match.Success && match.Groups.Count >= 2)
                    TradeManager.SetPartner(match.Groups[1].Value);
                break;

            case RegexAction.TradeGilIn:
                if (match.Success && match.Groups.Count >= 2)
                {
                    TradeManager.AddGil(match.Groups[1].Value, true);
                    Plugin.Instance.GetMainWindow().AddDebugLog(
                        $"[RegexEngine] TradeGilIn matched: +{match.Groups[1].Value}");
                }
                break;

            case RegexAction.TradeGilOut:
                if (match.Success && match.Groups.Count >= 2)
                {
                    TradeManager.AddGil(match.Groups[1].Value, false);
                    Plugin.Instance.GetMainWindow().AddDebugLog(
                        $"[RegexEngine] TradeGilOut matched: -{match.Groups[1].Value}");
                }
                break;

            case RegexAction.TradeCommit:
                TradeManager.CommitTrade(players);
                Plugin.Instance.GetMainWindow().AddDebugLog(
                    "[RegexEngine] TradeCommit matched");
                break;

            case RegexAction.TradeCancel:
                TradeManager.Reset();
                break;

            case RegexAction.BetInformationChange:
                if (p != null) p.HighlightBet = true;
                break;

            case RegexAction.WantHit:
                if (!cfg.EnableAutomation || !cfg.ShowAutoRunButton || !cfg.AutoRun)
                {
                    if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                        p.HighlightHit = true;
                    break;
                }
                if (p != null && p.IsCurrentTurn && !CommandExecutor.IsRunning
                    && GameEngine.CurrentPhase == GamePhase.PlayersTurn
                    && p.HasInitialHandDealt && p.Hands.Count > 0)
                {
                    var hand = p.Hands[p.CurrentHandIndex];
                    var (min, _) = p.CalculatePoints(p.CurrentHandIndex);
                    if (min < 21 && !hand.IsDoubleDown && !hand.IsStand)
                    {
                        GameLog.PushSnapshot(players, dealer, GameEngine.CurrentPhase, $"RegexHit:{p.Name}");
                        Task.Run(() => GameEngine.ActionHit(p, cfg, players));
                    }
                }
                break;

            case RegexAction.WantStand:
                if (!cfg.EnableAutomation || !cfg.ShowAutoRunButton || !cfg.AutoRun)
                {
                    if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                        p.HighlightStand = true;
                    break;
                }
                if (p != null && p.IsCurrentTurn && !CommandExecutor.IsRunning
                    && GameEngine.CurrentPhase == GamePhase.PlayersTurn
                    && p.HasInitialHandDealt && p.Hands.Count > 0)
                {
                    var hand = p.Hands[p.CurrentHandIndex];
                    if (!hand.IsStand && !hand.IsBust)
                    {
                        GameLog.PushSnapshot(players, dealer, GameEngine.CurrentPhase, $"RegexStand:{p.Name}");
                        Task.Run(() => GameEngine.ActionStand(p, cfg, players));
                    }
                }
                break;

            case RegexAction.WantDD:
                if (!cfg.EnableAutomation || !cfg.ShowAutoRunButton || !cfg.AutoRun)
                {
                    if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                        p.HighlightDD = true;
                    break;
                }
                if (p != null && p.IsCurrentTurn && !CommandExecutor.IsRunning
                    && GameEngine.CurrentPhase == GamePhase.PlayersTurn
                    && p.HasInitialHandDealt && p.Hands.Count > 0)
                {
                    var hand = p.Hands[p.CurrentHandIndex];
                    var (min, _) = p.CalculatePoints(p.CurrentHandIndex);
                    if (min < 21 && !hand.IsDoubleDown && !hand.IsStand
                        && hand.Cards.Count == 2
                        && cfg.EnableDoubleDown
                        && !(p.Hands.Count > 1 && !cfg.AllowDoubleDownAfterSplit))
                    {
                        GameLog.PushSnapshot(players, dealer, GameEngine.CurrentPhase, $"RegexDD:{p.Name}");
                        Task.Run(() => GameEngine.ActionDD(p, cfg, players));
                    }
                }
                break;

            case RegexAction.WantSplit:
                if (!cfg.EnableAutomation || !cfg.ShowAutoRunButton || !cfg.AutoRun)
                {
                    if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                        p.HighlightSplit = true;
                    break;
                }
                if (p != null && p.IsCurrentTurn && !CommandExecutor.IsRunning
                    && GameEngine.CurrentPhase == GamePhase.PlayersTurn
                    && p.HasInitialHandDealt && p.Hands.Count > 0)
                {
                    var hand = p.Hands[p.CurrentHandIndex];
                    var (min, _) = p.CalculatePoints(p.CurrentHandIndex);
                    if (min < 21 && !hand.IsDoubleDown && !hand.IsStand
                        && cfg.EnableSplit
                        && hand.Cards.Count == 2 && p.Hands.Count < cfg.MaxHandsPerPlayer)
                    {
                        bool canSplit = cfg.IdenticalSplitOnly
                            ? hand.Cards[0].Value == hand.Cards[1].Value
                            : PlayerState.GetCardScoreValue(hand.Cards[0].Value) == PlayerState.GetCardScoreValue(hand.Cards[1].Value);
                        if (canSplit)
                        {
                            GameLog.PushSnapshot(players, dealer, GameEngine.CurrentPhase, $"RegexSplit:{p.Name}");
                            Task.Run(() => GameEngine.ActionSplit(p, cfg, players));
                        }
                    }
                }
                break;

            case RegexAction.BankOut:
                if (p != null) p.HighlightPay = true;
                break;

            case RegexAction.TakeBatch:
                var batch = cfg.MessageBatches.FirstOrDefault(b => b.Name == entry.ActionParam);
                if (batch != null)
                {
                    string rawText = batch.GetNextMessage(); // Erstmal zum test auslassen ... .Replace("<t>", msg.Name);
                    string processedText = VariableManager.ProcessMessage(rawText);
                    ChatCommandRouter.Send($"/p {processedText}", cfg, $"Batch:{batch.Name}->{msg.Name}");
                }
                break;

            // Persistent highlights — always set
            case RegexAction.HighlightBet:     if (p != null) p.HighlightBet = true; break;
            case RegexAction.HighlightPayout:  if (p != null) p.HighlightPay = true; break;
            case RegexAction.HighlightAlias:   if (p != null) p.HighlightAlias = true; break;
            case RegexAction.HighlightPause:   if (p != null) p.HighlightPause = true; break;
            case RegexAction.HighlightLeave:   if (p != null) p.HighlightLeave = true; break;
            case RegexAction.HighlightJoin:    if (p != null) p.HighlightJoin = true; break;

            // Once-consistent highlights — only set if none in group is active
            case RegexAction.HighlightHit:
                if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                    p.HighlightHit = true;
                break;
            case RegexAction.HighlightStand:
                if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                    p.HighlightStand = true;
                break;
            case RegexAction.HighlightDD:
                if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                    p.HighlightDD = true;
                break;
            case RegexAction.HighlightSplit:
                if (p != null && !p.HighlightHit && !p.HighlightStand && !p.HighlightDD && !p.HighlightSplit)
                    p.HighlightSplit = true;
                break;

            case RegexAction.NextRound:
            {
                var phase = GameEngine.CurrentPhase;
                if (phase != GamePhase.Waiting && phase != GamePhase.Payout)
                    break;
                if (p == null || !p.IsActivePlayer || p.IsOnHold)
                    break;
                _nextRoundVotes.Add(p.Name);
                var activePlayers = players.Where(pl => pl.IsActivePlayer && !pl.IsOnHold).ToList();
                if (activePlayers.All(pl => pl.ReadySkip || _nextRoundVotes.Contains(pl.Name)))
                {
                    _nextRoundVotes.Clear();
                    if (activePlayers.Count == 1 && cfg.AutostartRoundOnlyOnMultiplePlayers)
                    {
                        Plugin.Instance.GetMainWindow().SetHighlightNewRound();
                    }
                    else if (cfg.EnableAutomation && cfg.ShowAutoRunButton && cfg.AutoRun)
                    {
                        Task.Run(() => GameEngine.StartInitialDeal(players, cfg));
                    }
                    else
                    {
                        Plugin.Instance.GetMainWindow().SetHighlightNewRound();
                    }
                }
                break;
            }

            case RegexAction.ExecuteOwnButton:
            {
                if (string.IsNullOrWhiteSpace(entry.ActionParam)) break;
                if (CommandExecutor.IsRunning) break;
                var targetOwnGroup = cfg.CustomCommandGroups.FirstOrDefault(
                    g => g.Name.Equals(entry.ActionParam, StringComparison.OrdinalIgnoreCase));
                if (targetOwnGroup != null && !targetOwnGroup.IsActive)
                {
                    Plugin.Instance.GetMainWindow().AddDebugLog(
                        $"[RegexEngine] ExecuteOwnButton '{entry.ActionParam}' skipped (inactive)");
                    break;
                }
                var obWindow = Plugin.Instance.GetMainWindow();
                var targetName = p?.DisplayName ?? msg.Name;
                var groupName = entry.ActionParam;
                obWindow.AddDebugLog($"[RegexEngine] ExecuteOwnButton '{groupName}' for {targetName}");
                var capturedP = p;
                Task.Run(async () =>
                {
                    if (capturedP != null)
                    {
                        GameEngine.TargetPlayer(capturedP.Name);
                        VariableManager.SetPlayerVariables(capturedP);
                    }
                    await CommandExecutor.ExecuteGroup(groupName, targetName, cfg);
                    GameEngine.TargetPlayer(obWindow.GetDealer().Name);
                });
                break;
            }

            case RegexAction.BankTell:
            {
                var btWindow = Plugin.Instance.GetMainWindow();
                var phase = GameEngine.CurrentPhase;

                if (phase != GamePhase.Waiting && phase != GamePhase.Payout)
                {
                    btWindow.AddDebugLog($"[RegexEngine] BankTell blocked: wrong phase {phase}");
                    break;
                }

                if (p == null)
                {
                    btWindow.AddDebugLog($"[RegexEngine] BankTell blocked: player not found for '{msg.Name}'");
                    break;
                }

                if (!p.IsActivePlayer || p.IsOnHold || p.IsOnBench || p.JoinedMidRound)
                {
                    btWindow.AddDebugLog($"[RegexEngine] BankTell blocked: {p.DisplayName} not active");
                    break;
                }

                if (!cfg.EnableAutomation || !cfg.ShowAutoRunButton || !cfg.AutoRun)
                {
                    p.HighlightTell = true;
                    btWindow.AddDebugLog($"[RegexEngine] BankTell highlight set for {p.DisplayName} (AutoRun off)");
                    break;
                }

                btWindow.AddDebugLog($"[RegexEngine] BankTell executing for {p.DisplayName}");
                p.HighlightTell = false;
                var capturedPlayer = p;
                Task.Run(async () =>
                {
                    GameEngine.TargetPlayer(capturedPlayer.Name);
                    VariableManager.SetPlayerVariables(capturedPlayer);
                    await CommandExecutor.ExecuteGroup("BankTell", capturedPlayer.DisplayName, cfg);
                    GameEngine.TargetPlayer(btWindow.GetDealer().Name);
                });
                break;
            }
        }
    }

    private static string SanitizeForRegex(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        bool lastWasSpace = false;
        foreach (var ch in input)
        {
            if (ch >= IconLetterStart && ch <= IconLetterEnd)
            {
                sb.Append('{');
                sb.Append((char)('A' + (ch - IconLetterStart)));
                sb.Append('}');
                lastWasSpace = false;
                continue;
            }

            if (ch >= '\uE000' && ch <= '\uF8FF')
                continue;

            if (ch == ' ')
            {
                if (lastWasSpace) continue;
                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }
            sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    private static string SanitizePatternForRegex(string pattern)
    {
        var sb = new System.Text.StringBuilder(pattern.Length);
        foreach (var ch in pattern)
        {
            if (ch >= IconLetterStart && ch <= IconLetterEnd)
            {
                sb.Append(@"\{");
                sb.Append((char)('A' + (ch - IconLetterStart)));
                sb.Append(@"\}");
                continue;
            }
            if (ch >= '\uE000' && ch <= '\uF8FF')
                continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static int? MapValue(int rolled)
    {
        if (rolled >= 1 && rolled <= 13) return rolled;
        return null;
    }
}
