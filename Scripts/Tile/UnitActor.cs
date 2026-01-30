using Godot;
using System.Collections.Generic;

public enum UnitTeam
{
	Friendly,
	Enemy
}

public partial class UnitActor : Node3D
{
	public const string UnitGroup = "tile_units";
	public const string IdleAnimation = "Idle_A";
	public const string MoveAnimation = "Walking_A";
	public const string SkillAnimation = "Melee_1H_Attack_Slice_Horizontal";

	[Export] public Vector2I TilePosition = new(0, 0);
	[Export] public Vector2I Size = new(2, 2);
	[Export] public int MovePoints = 4;
	[Export] public int MaxHp = 20;
	[Export] public int Hp = 20;
	[Export] public int MaxArmor = 10;
	[Export] public int Armor = 10;
	[Export] public int Attack = 5;
	[Export] public int Accuracy = 75;
	[Export] public int Evasion = 10;
	[Export] public UnitTeam Team = UnitTeam.Friendly;

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

	public void PlaySkill(string animationName)
	{
		if (_animator == null)
		{
			return;
		}

		var anim = string.IsNullOrWhiteSpace(animationName) ? SkillAnimation : animationName;
		if (_animator.HasAnimation(anim))
		{
			_animator.Play(anim);
		}
		else if (_animator.HasAnimation(SkillAnimation))
		{
			_animator.Play(SkillAnimation);
		}
	}

	public CombatStats BuildStats()
	{
		return new CombatStats
		{
			MaxHp = MaxHp,
			Hp = Hp,
			MaxArmor = MaxArmor,
			Armor = Armor,
			Attack = Attack,
			Accuracy = Accuracy,
			Evasion = Evasion
		};
	}

	public void ApplyStats(CombatStats stats)
	{
		MaxHp = stats.MaxHp;
		Hp = stats.Hp;
		MaxArmor = stats.MaxArmor;
		Armor = stats.Armor;
		Attack = stats.Attack;
		Accuracy = stats.Accuracy;
		Evasion = stats.Evasion;
	}
}
