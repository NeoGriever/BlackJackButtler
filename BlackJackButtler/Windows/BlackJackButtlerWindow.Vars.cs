using System.Numerics;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawVarsPage()
    {
        ImGui.TextUnformatted("Session Variables");
        ImGui.Separator();
        ImGui.TextDisabled("These variables are stored for the current session and can be used in messages via ${name}.");
        ImGui.Spacing();

        if (ImGui.BeginTable("bjb_vars_table", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Variable Name", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Current Value", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Copy ${}", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Copy $${}", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableHeadersRow();

            for (int i = 0; i < VariableManager.Variables.Count; i++)
            {
                var v = VariableManager.Variables[i];
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (v.IsManual)
                {
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputText($"##vname_{v.Name}", ref v.Name, 64);
                }
                else
                {
                    ImGui.TextUnformatted(v.Name);
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText($"##vval_{v.Name}", ref v.Value, 256);

                ImGui.TableNextColumn();
                if (ImGui.Button($"Copy##c1_{v.Name}", new Vector2(-1, 0)))
                {
                    ImGui.SetClipboardText("${" + v.Name + "}");
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"Copy##c2_{v.Name}", new Vector2(-1, 0)))
                {
                    ImGui.SetClipboardText("$${" + v.Name + "}");
                }

                ImGui.TableNextColumn();
                if (ImGui.Button($"X##del_{v.Name}", new Vector2(-1, 0)))
                {
                    VariableManager.Variables.RemoveAt(i);
                    break;
                }
            }
            ImGui.EndTable();
        }

        ImGui.Spacing();
        if (ImGui.Button("+ Add Manual Variable"))
        {
            VariableManager.Variables.Add(new SessionVariable { Name = "new_var", Value = "", IsManual = true });
        }
    }
}
