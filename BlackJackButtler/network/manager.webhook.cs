using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class WebhookManager
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task PostRoundResult(WebhookEntry webhook, PlayerState dealer, List<PlayerState> players, Configuration cfg)
    {
        try
        {
            int dealerScore = dealer.GetBestScore(0);
            bool dealerBust = dealer.Hands.Count > 0 && dealer.Hands[0].IsBust;

            var description = $"**Dealer** has {dealer.GetCardsString(0)} - total **{dealerScore}**";
            if (dealerBust) description += " (BUST)";

            var fields = new List<object>();

            foreach (var p in players)
            {
                string playerName = TradeManager.StripWorldSuffix(p.DisplayName);

                for (int h = 0; h < p.Hands.Count; h++)
                {
                    var hand = p.Hands[h];
                    if (hand.Cards.Count == 0) continue;

                    int pScore = p.GetBestScore(h);
                    string cards = p.GetCardsString(h);

                    string result;
                    string amountStr = "";

                    if (hand.IsBust)
                    {
                        result = "BUST";
                        if (webhook.ShowBetAmounts) amountStr = $" (-{hand.Bet:N0})";
                    }
                    else if (dealerBust || pScore > dealerScore || hand.IsCharlie || (cfg.PlayerBJWinsOnTie && pScore == 21 && dealerScore == 21))
                    {
                        float mult = cfg.MultiplierNormalWin;
                        if (hand.IsNaturalBlackJack) mult = cfg.MultiplierBlackjackWin;
                        else if (hand.IsCharlie) mult = cfg.MultiplierBlackjackWin;
                        else if (pScore == 21) mult = cfg.MultiplierDirtyBlackjackWin;

                        long winAmount = (long)(hand.Bet * mult);

                        if (hand.IsNaturalBlackJack)
                            result = "BLACKJACK";
                        else if (hand.IsCharlie)
                            result = "CHARLIE";
                        else
                            result = "WIN";

                        if (webhook.ShowBetAmounts) amountStr = $" (+{winAmount:N0})";
                    }
                    else if (pScore == dealerScore)
                    {
                        result = "PUSH";
                    }
                    else
                    {
                        result = "LOST";
                        if (webhook.ShowBetAmounts) amountStr = $" (-{hand.Bet:N0})";
                    }

                    string handLabel = p.Hands.Count > 1 ? $" (Hand {h + 1})" : "";
                    string value = $"{cards} - total **{pScore}** | **{result}**{amountStr}";

                    fields.Add(new { name = $"{playerName}{handLabel}", value, inline = false });
                }
            }

            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "BlackJack - Round Results",
                        color = 16766720,
                        description,
                        fields,
                        footer = new { text = "BlackJack Buttler" },
                        timestamp = DateTime.UtcNow.ToString("o")
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(webhook.Url, content);

            if (!response.IsSuccessStatusCode)
                Plugin.Log.Error($"[WebhookManager] Discord returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[WebhookManager] Failed to post round result: {ex.Message}");
        }
    }
}
