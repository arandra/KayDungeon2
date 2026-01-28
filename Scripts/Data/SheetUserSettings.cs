using Godot;

public static class SheetUserSettings
{
    private const string ServiceAccountPathFile = "user://sheet_service_account_path.txt";

    public static string LoadServiceAccountPath()
    {
        if (!FileAccess.FileExists(ServiceAccountPathFile))
        {
            return "";
        }

        using var file = FileAccess.Open(ServiceAccountPathFile, FileAccess.ModeFlags.Read);
        return file.GetAsText().Trim();
    }

    public static void SaveServiceAccountPath(string path)
    {
        using var file = FileAccess.Open(ServiceAccountPathFile, FileAccess.ModeFlags.Write);
        file.StoreString(path ?? "");
    }
}
