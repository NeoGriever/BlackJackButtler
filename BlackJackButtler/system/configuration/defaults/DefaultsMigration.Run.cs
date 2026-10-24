using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    public static bool RunMigration(Configuration config)
    {
        bool coreChanged = RunMigrationCore(config);
        bool protectedAdded = EnsureProtectedEntriesExist(config);
        return coreChanged || protectedAdded;
    }

    private static bool RunMigrationCore(Configuration config)
    {
        try
        {
            var assemblyVersion = GetAssemblyVersion();
            var snapshot = LoadSnapshot();

            if (snapshot == null)
            {
                bool isExistingUser = config.DefaultBatchesSeeded
                                   || config.DefaultRegexSeeded
                                   || config.DefaultCommandsSeeded;

                if (isExistingUser)
                {
                    var fresh = CreateFreshSnapshot(assemblyVersion);
                    SaveSnapshot(fresh);
                    Plugin.Log.Information("[DefaultsMigration] Existing user detected. Created defaults.json without modifying config.");
                    return false;
                }
                else
                {
                    SeedDefaultsFromEmbeddedConfiguration(config);
                    var fresh = CreateFreshSnapshot(assemblyVersion);
                    SaveSnapshot(fresh);
                    Plugin.Log.Information("[DefaultsMigration] New installation detected. Seeded embedded defaults and created defaults.json.");
                    return true;
                }
            }
            else
            {
                if (snapshot.Version == assemblyVersion)
                {
                    Plugin.Log.Debug("[DefaultsMigration] Version matches. No migration needed.");
                    return false;
                }

                Plugin.Log.Information($"[DefaultsMigration] Version changed: {snapshot.Version} -> {assemblyVersion}. Running migration.");
                MigrateNotifyGroupNames(config, snapshot);
                bool changed = MergeNewEntries(snapshot, config);
                UpdateSnapshotFile(snapshot, assemblyVersion);
                return changed;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[DefaultsMigration] Migration failed: {ex.Message}");
            return config.EnsureDefaultsOnce();
        }
    }
}
