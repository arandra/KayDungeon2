using Godot;

[Tool]
public partial class SheetSyncPlugin : EditorPlugin
{
    private EditorFileDialog _fileDialog;

    public override void _EnterTree()
    {
        AddToolMenuItem("Sheets/Sync Google Sheets", new Callable(this, nameof(SyncSheets)));
        AddToolMenuItem("Sheets/Set Service Account Key Path", new Callable(this, nameof(OpenServiceAccountDialog)));
        AddToolMenuItem("Sheets/Reload Local Data", new Callable(this, nameof(ReloadLocalData)));

        _fileDialog = new EditorFileDialog
        {
            FileMode = EditorFileDialog.FileModeEnum.OpenFile,
            Access = EditorFileDialog.AccessEnum.Filesystem,
            Filters = new[] { "*.json ; Service Account JSON" }
        };
        _fileDialog.FileSelected += OnServiceAccountSelected;
        AddChild(_fileDialog);
    }

    public override void _ExitTree()
    {
        RemoveToolMenuItem("Sheets/Sync Google Sheets");
        RemoveToolMenuItem("Sheets/Set Service Account Key Path");
        RemoveToolMenuItem("Sheets/Reload Local Data");

        if (_fileDialog != null)
        {
            _fileDialog.FileSelected -= OnServiceAccountSelected;
            _fileDialog.QueueFree();
        }
    }

    private void OpenServiceAccountDialog()
    {
        _fileDialog?.PopupCenteredRatio(0.6f);
    }

    private void OnServiceAccountSelected(string path)
    {
        SheetUserSettings.SaveServiceAccountPath(path);
        GD.Print($"Sheet Sync: Service account path saved: {path}");
    }

    private void SyncSheets()
    {
        if (SheetSyncService.SyncFromGoogle(out var message))
        {
            GD.Print(message);
        }
        else
        {
            GD.PushWarning(message);
        }
    }

    private void ReloadLocalData()
    {
        if (LocalDataRegistry.LoadAll(out var message))
        {
            SkillCatalog.LoadFromLocalData();
            GD.Print(message);
        }
        else
        {
            GD.PushWarning(message);
        }
    }
}
