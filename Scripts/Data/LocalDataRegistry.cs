using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class LocalDataRegistry
{
    private const string DataFolder = "res://Data/Sheets";

    private static readonly Dictionary<string, SheetTable> TablesInternal = new();

    public static IReadOnlyDictionary<string, SheetTable> Tables => TablesInternal;

    public static event Action Reloaded;

    public static bool LoadAll(out string message)
    {
        TablesInternal.Clear();

        var dir = DirAccess.Open(DataFolder);
        if (dir == null)
        {
            message = $"LocalDataRegistry: Data folder not found: {DataFolder}";
            return false;
        }

        var files = dir.GetFiles();
        foreach (var file in files)
        {
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (file.Equals("sheet_config.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = $"{DataFolder}/{file}";
            try
            {
                using var dataFile = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                var json = dataFile.GetAsText();
                var table = JsonSerializer.Deserialize<SheetTable>(json);
                if (table == null)
                {
                    GD.PushWarning($"LocalDataRegistry: Failed to parse {path}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(table.Name))
                {
                    table.Name = Path.GetFileNameWithoutExtension(file);
                }

                TablesInternal[table.Name] = table;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"LocalDataRegistry: Error loading {path}: {ex.Message}");
            }
        }

        Reloaded?.Invoke();
        message = $"LocalDataRegistry: Loaded {TablesInternal.Count} tables.";
        return true;
    }
}
