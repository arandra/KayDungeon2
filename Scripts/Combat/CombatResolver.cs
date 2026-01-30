using Godot;
using System;
using System.Collections.Generic;

public static class CombatResolver
{
    public static readonly RandomNumberGenerator Rng = new();

    public static int ClampHitChance(int accuracy, int evasion)
    {
        var chance = accuracy - evasion;
        return Mathf.Clamp(chance, 5, 95);
    }

    public static bool RollHit(int accuracy, int evasion)
    {
        var chance = ClampHitChance(accuracy, evasion);
        var roll = Rng.RandiRange(1, 100);
        return roll <= chance;
    }

    public static int ComputeBaseDamage(CombatStats attacker, SkillData skill)
    {
        var raw = attacker.Attack * skill.Power;
        return (int)MathF.Round(raw);
    }

    public static int ApplyDamage(CombatStats defender, int damage, SkillData skill)
    {
        var beforeArmor = defender.Armor;
        var beforeHp = defender.Hp;

        if (damage <= 0)
        {
            GD.Print($"[Combat] {skill.Name} damage <= 0, no effect.");
            return 0;
        }

        if (skill.Traits.HasFlag(SkillTrait.IgnoreArmor))
        {
            if (skill.Traits.HasFlag(SkillTrait.BonusHpMultiplier))
            {
                var before = damage;
                damage = (int)MathF.Round(damage * skill.BonusHpMultiplier);
                GD.Print($"[Combat] {skill.Name} ignore armor, bonus hp x{skill.BonusHpMultiplier}: {before}->{damage}");
            }
            defender.Hp = Math.Max(0, defender.Hp - damage);
            GD.Print($"[Combat] {skill.Name} ignore armor, hp {beforeHp}->{defender.Hp}");
            return damage;
        }

        var remaining = damage;
        if (defender.Armor > 0)
        {
            var absorbed = Math.Min(defender.Armor, remaining);
            defender.Armor -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0)
        {
            if (skill.Traits.HasFlag(SkillTrait.BonusHpMultiplier))
            {
                var before = remaining;
                remaining = (int)MathF.Round(remaining * skill.BonusHpMultiplier);
                GD.Print($"[Combat] {skill.Name} bonus hp x{skill.BonusHpMultiplier}: {before}->{remaining}");
            }
            defender.Hp = Math.Max(0, defender.Hp - remaining);
        }

        GD.Print($"[Combat] {skill.Name} damage {damage} -> armor {beforeArmor}->{defender.Armor}, hp {beforeHp}->{defender.Hp}");
        return damage;
    }

    public static int ResolveSkillHit(CombatStats attacker, CombatStats defender, SkillData skill)
    {
        var baseDamage = ComputeBaseDamage(attacker, skill);
        GD.Print($"[Combat] {skill.Name} base damage {baseDamage} (atk {attacker.Attack} * power {skill.Power})");
        var damage = baseDamage;

        if (skill.Traits.HasFlag(SkillTrait.BonusDamage))
        {
            var before = damage;
            damage = (int)MathF.Round(damage * skill.BonusDamageMultiplier);
            GD.Print($"[Combat] {skill.Name} bonus damage x{skill.BonusDamageMultiplier}: {before}->{damage}");
        }

        return ApplyDamage(defender, damage, skill);
    }

    public static int ResolveSkill(CombatStats attacker, CombatStats defender, SkillData skill)
    {
        var hits = Math.Max(1, skill.Hits);
        var total = 0;

        for (int i = 0; i < hits; i++)
        {
            var accuracy = attacker.Accuracy + (int)MathF.Round(skill.AccuracyBonus);
            var chance = ClampHitChance(accuracy, defender.Evasion);
            var roll = Rng.RandiRange(1, 100);
            var hit = roll <= chance;
            GD.Print($"[Combat] {skill.Name} hit roll {roll} <= {chance} (acc {accuracy} eva {defender.Evasion}) => {(hit ? "HIT" : "MISS")}");
            if (!hit)
            {
                continue;
            }

            total += ResolveSkillHit(attacker, defender, skill);
        }

        return total;
    }
}
