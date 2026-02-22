using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _newWebhookName = "New Webhook";
    private string _newWebhookUrl = string.Empty;

    private void DrawWebhooksPage()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "Discord Webhooks");
        ImGui.Separator();
        ImGui.TextWrapped("Configure Discord webhooks to automatically post round results to a channel.");
        ImGui.Spacing();

        // --- Add Webhook Section ---
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Add Webhook");

        ImGui.SetNextItemWidth(300f);
        ImGui.InputText("Name##new_wh_name", ref _newWebhookName, 64);

        ImGui.SetNextItemWidth(500f);
        ImGui.InputText("URL##new_wh_url", ref _newWebhookUrl, 256);

        bool urlValid = _newWebhookUrl.StartsWith("https://discord.com/api/webhooks/");
        bool nameValid = !string.IsNullOrWhiteSpace(_newWebhookName);
        bool canAdd = urlValid && nameValid;

        if (!canAdd) ImGui.BeginDisabled();
        if (BJBGui.Button("Add Webhook"))
        {
            _config.Webhooks.Add(new WebhookEntry
            {
                Name = _newWebhookName.Trim(),
                Url = _newWebhookUrl.Trim(),
            });
            _newWebhookName = "New Webhook";
            _newWebhookUrl = string.Empty;
            _save();
        }
        if (!canAdd) ImGui.EndDisabled();

        if (!urlValid && _newWebhookUrl.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "URL must start with https://discord.com/api/webhooks/");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // --- Existing Webhooks ---
        int deleteIndex = -1;
        for (int i = 0; i < _config.Webhooks.Count; i++)
        {
            var wh = _config.Webhooks[i];
            string headerLabel = wh.Enabled ? wh.Name : $"{wh.Name} (disabled)";
            if (ImGui.CollapsingHeader($"{headerLabel}##wh_{i}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.PushID($"wh_edit_{i}");

                if (ImGui.Checkbox("Enabled", ref wh.Enabled))
                {
                    _selectedWebhookIndex = -1;
                    _save();
                }

                ImGui.SetNextItemWidth(300f);
                if (ImGui.InputText("Name", ref wh.Name, 64)) _save();

                ImGui.SetNextItemWidth(500f);
                if (ImGui.InputText("URL", ref wh.Url, 256)) _save();

                if (wh.Url.Length > 0 && !wh.Url.StartsWith("https://discord.com/api/webhooks/"))
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "URL must start with https://discord.com/api/webhooks/");

                if (ImGui.Checkbox("Show win/loss amounts", ref wh.ShowBetAmounts)) _save();

                var io = ImGui.GetIO();
                bool ctrlHeld = io.KeyCtrl;

                if (!ctrlHeld) ImGui.BeginDisabled();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.1f, 0.1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.2f, 0.2f, 1f));
                if (BJBGui.Button("Delete Webhook")) deleteIndex = i;
                ImGui.PopStyleColor(2);
                if (!ctrlHeld) ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Hold Ctrl to delete");

                ImGui.PopID();
                ImGui.Spacing();
            }
        }

        if (deleteIndex >= 0)
        {
            _config.Webhooks.RemoveAt(deleteIndex);
            _selectedWebhookIndex = -1;
            _save();
        }
    }
}
