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
	private readonly Color _selectedColor = new(1f, 1f, 0.6f, 1f);
	private readonly Color _normalColor = new(1f, 1f, 1f, 1f);

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
		if (unit != null)
		{
			SetSelection(0);
		}
	}

	public void SetSkillEnabled(bool enabled)
	{
		if (_skillButton != null)
		{
			_skillButton.Disabled = !enabled;
		}
	}

	public void SetSelection(int index)
	{
		if (_moveButton != null)
		{
			_moveButton.Modulate = index == 0 ? _selectedColor : _normalColor;
		}
		if (_skillButton != null)
		{
			_skillButton.Modulate = index == 1 ? _selectedColor : _normalColor;
		}
	}
}
