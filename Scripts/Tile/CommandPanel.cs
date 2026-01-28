using Godot;

public partial class CommandPanel : PanelContainer
{
    [Signal]
    public delegate void MovePressedEventHandler();

    [Signal]
    public delegate void SkillPressedEventHandler();

    private Label _title;
    private Button _moveButton;
    private Button _skillButton;

    public override void _Ready()
    {
        _title = GetNodeOrNull<Label>("VBox/Title");
        _moveButton = GetNodeOrNull<Button>("VBox/MoveButton");
        _skillButton = GetNodeOrNull<Button>("VBox/SkillButton");

        if (_moveButton != null)
        {
            _moveButton.Pressed += () => EmitSignal(SignalName.MovePressed);
        }

        if (_skillButton != null)
        {
            _skillButton.Pressed += () => EmitSignal(SignalName.SkillPressed);
        }
    }

    public void ShowFor(UnitActor unit)
    {
        if (_title != null)
        {
            _title.Text = unit != null ? $"명령: {unit.Name}" : "명령";
        }

        Visible = unit != null;
    }

    public void SetSkillEnabled(bool enabled)
    {
        if (_skillButton != null)
        {
            _skillButton.Disabled = !enabled;
        }
    }
}
