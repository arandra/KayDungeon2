using Godot;
using System.Collections.Generic;

public partial class UnitActor : Node3D
{
	public const string UnitGroup = "tile_units";
	public const string IdleAnimation = "Idle_A";
	public const string MoveAnimation = "Walking_A";

	[Export] public Vector2I TilePosition = new(0, 0);
	[Export] public Vector2I Size = new(2, 2);
	[Export] public int MovePoints = 4;

	private AnimationPlayer _animator;

	public override void _Ready()
	{
		AddToGroup(UnitGroup);
		_animator = GetNodeOrNull<AnimationPlayer>("CharacterAnimator");
		PlayIdle();
	}

	public IEnumerable<Vector2I> GetFootprintTiles(Vector2I anchor)
	{
		for (int x = 0; x < Size.X; x++)
		{
			for (int y = 0; y < Size.Y; y++)
			{
				yield return new Vector2I(anchor.X + x, anchor.Y + y);
			}
		}
	}

	public void PlayMove()
	{
		if (_animator == null)
		{
			return;
		}

		if (_animator.HasAnimation(MoveAnimation))
		{
			_animator.Play(MoveAnimation);
		}
	}

	public void PlayIdle()
	{
		if (_animator == null)
		{
			return;
		}

		if (_animator.HasAnimation(IdleAnimation))
		{
			_animator.Play(IdleAnimation);
		}
	}
}
