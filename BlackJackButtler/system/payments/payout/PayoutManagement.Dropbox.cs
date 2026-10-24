using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BlackJackButtler.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace BlackJackButtler;

public static partial class PayoutManagement
{
    private static bool ShouldUseDropbox(DropboxStatus status)
    {
#if DEBUG
        return Plugin.Instance.Configuration.DebugPayoutExecutor switch
        {
            DebugPayoutExecutorVariant.ModularTrade => false,
            DebugPayoutExecutorVariant.Dropbox => status.Active,
            _ => status.Installed && status.Active,
        };
#else
        return status.Installed && status.Active;
#endif
    }

    public static DropboxStatus GetDropboxStatus()
    {
        var active = false;
        var installed = false;

        try
        {
            active = DalamudReflector.TryGetDalamudPlugin(
                "Dropbox",
                out _,
                out _,
                suppressErrors: true,
                ignoreCache: true
            );
        }
        catch (Exception ex)
        {
            Trace($"Dropbox active-state query failed: {ex.GetBaseException().Message}");
        }

        try
        {
            var getPluginManager = typeof(DalamudReflector).GetMethod(
                "GetPluginManager",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );
            var pluginManager = getPluginManager?.Invoke(null, null);
            var installedPlugins = pluginManager?.GetType().GetProperty(
                "InstalledPlugins",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )?.GetValue(pluginManager)
                as IEnumerable;

            if (installedPlugins is not null)
            {
                foreach (var plugin in installedPlugins)
                {
                    if (plugin is not null && IsDropboxPlugin(plugin))
                    {
                        installed = true;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace($"Dropbox installation-state query failed: {ex.GetBaseException().Message}");
        }

        return new DropboxStatus(installed || active, active);
    }

    private static bool IsDropboxPlugin(object plugin)
    {
        var type = plugin.GetType();
        var internalName = type.GetProperty("InternalName")?.GetValue(plugin) as string
            ?? type.GetField("InternalName")?.GetValue(plugin) as string;
        if (string.Equals(internalName, "Dropbox", StringComparison.OrdinalIgnoreCase))
            return true;

        var manifest = type.GetProperty("Manifest")?.GetValue(plugin);
        if (manifest is null)
            return false;

        var manifestType = manifest.GetType();
        internalName = manifestType.GetProperty("InternalName")?.GetValue(manifest) as string
            ?? manifestType.GetField("InternalName")?.GetValue(manifest) as string;
        return string.Equals(internalName, "Dropbox", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryStartDropboxPayout(PlayerState player, int gil)
    {
        Trace($"Trying external Dropbox executor for {player.DisplayName}, gil={gil}.");
        if (gil < 1 || gil > MaxGilPerTrade) return false;

        try
        {
            var taskApis = GetDropboxTaskApis().ToList();
            if (taskApis.Count == 0)
            {
                Trace("External Dropbox task API was not found.");
                DebugLog("[PayoutManagement] Dropbox task API not found");
                return false;
            }

            if (IsDropboxCommandRegistered())
                OpenDropboxWindow();

            if (!TargetPayoutPlayerImmediate(player))
            {
                DebugLog("[PayoutManagement] Dropbox target/focus target could not be set");
                return false;
            }

            foreach (var (queueEntryType, enqueue) in taskApis)
            {
                try
                {
                    var emptyEntries = Array.CreateInstance(queueEntryType, 0);
                    enqueue.Invoke(null, new object[] { emptyEntries, gil });
                    DebugLog(
                        $"[PayoutManagement] Dropbox task invoked directly ({enqueue.DeclaringType?.Assembly.GetName().Name})"
                    );
                    Trace($"External Dropbox task invocation succeeded: assembly={enqueue.DeclaringType?.Assembly.GetName().Name}.");
                    return true;
                }
                catch (Exception ex)
                {
                    DebugLog(
                        $"[PayoutManagement] Dropbox task invocation failed ({enqueue.DeclaringType?.Assembly.GetName().Name}): {ex.GetBaseException().Message}"
                    );
                    Trace($"External Dropbox task invocation failed: {ex.GetBaseException().Message}");
                }
            }

            DebugLog("[PayoutManagement] All Dropbox task APIs failed");
            return false;
        }
        catch (Exception ex)
        {
            DebugLog($"[PayoutManagement] Dropbox start failed: {ex.GetBaseException().Message}");
            Trace($"External Dropbox start failed: {ex.GetBaseException().Message}");
            return false;
        }
    }

    private static IEnumerable<(Type QueueEntryType, MethodInfo Enqueue)> GetDropboxTaskApis()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var queueEntryType = assembly.GetType("Dropbox.QueueEntry", false);
            var taskType = assembly.GetType("Dropbox.TaskAddItemsToTrade", false);
            if (queueEntryType == null || taskType == null)
                continue;

            var queueEntryArrayType = queueEntryType.MakeArrayType();
            var enqueue = taskType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(
                    method =>
                    {
                        if (!method.Name.Equals("Enqueue", StringComparison.Ordinal))
                            return false;

                        var parameters = method.GetParameters();
                        return parameters.Length == 2
                            && parameters[0].ParameterType.IsAssignableFrom(queueEntryArrayType)
                            && parameters[1].ParameterType == typeof(int);
                    }
                );

            if (enqueue != null)
                yield return (queueEntryType, enqueue);
        }
    }

    private static bool IsDropboxCommandRegistered()
    {
        return Plugin.CommandManager.Commands.Keys.Any(k => k.Equals("/dropbox", StringComparison.OrdinalIgnoreCase));
    }

    private static void OpenDropboxWindow()
    {
        if (_openedDropboxUiForPayout)
            return;

        var opened = false;
        try
        {
            opened = Plugin.CommandManager.ProcessCommand("/dropbox");
        }
        catch (Exception ex)
        {
            DebugLog($"[PayoutManagement] /dropbox command failed: {ex.Message}");
        }

        DebugLog(
            opened ? "[PayoutManagement] /dropbox command dispatched" : "[PayoutManagement] /dropbox command was registered but dispatch returned false"
        );
        _openedDropboxUiForPayout = opened;
    }

    private static unsafe bool IsTradeOpen()
    {
        return Svc.Condition[ConditionFlag.TradeOpen]
            || (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon));
    }
}
