using System;
using System.Collections.Generic;

public enum SkillTargetTeam
{
    Friendly,
    Enemy,
    Self
}

public enum SkillTargetShape
{
    Single,
    Area
}

[Flags]
public enum SkillTrait
{
    None = 0,
    BonusDamage = 1 << 0,
    IgnoreArmor = 1 << 1,
    BonusHpMultiplier = 1 << 2
}

public class SkillData
{
    public string SkillId { get; set; } = "";
    public string Name { get; set; } = "";
    public SkillTargetTeam TargetTeam { get; set; } = SkillTargetTeam.Enemy;
    public SkillTargetShape TargetShape { get; set; } = SkillTargetShape.Single;
    public int RangeStep { get; set; }
    public int RadiusStep { get; set; }
    public int Hits { get; set; } = 1;
    public float Power { get; set; } = 1.0f;
    public float AccuracyBonus { get; set; }
    public string Animation { get; set; } = "";
    public float BonusDamageMultiplier { get; set; } = 1.0f;
    public float BonusHpMultiplier { get; set; } = 1.0f;
    public SkillTrait Traits { get; set; } = SkillTrait.None;
}
