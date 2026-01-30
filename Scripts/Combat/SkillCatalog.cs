using Godot;
using System;
using System.Collections.Generic;

public static class SkillCatalog
{
    private static readonly Dictionary<string, SkillData> Skills = new(StringComparer.OrdinalIgnoreCase);

    public static void LoadFromLocalData()
    {
        Skills.Clear();

        if (!LocalDataRegistry.Tables.TryGetValue("skills", out var table))
        {
            return;
        }

        var headerMap = BuildHeaderMap(table.Headers);
        foreach (var row in table.Rows)
        {
            var skill = ParseSkill(row, headerMap);
            if (skill == null || string.IsNullOrWhiteSpace(skill.SkillId))
            {
                continue;
            }

            Skills[skill.SkillId] = skill;
        }
    }

    public static SkillData GetDefaultSkill()
    {
        if (Skills.TryGetValue("basic_attack", out var skill))
        {
            return skill;
        }

        foreach (var entry in Skills.Values)
        {
            return entry;
        }

        return new SkillData
        {
            SkillId = "basic_attack",
            Name = "기본 공격",
            TargetTeam = SkillTargetTeam.Enemy,
            TargetShape = SkillTargetShape.Single,
            RangeStep = 1,
            RadiusStep = 0,
            Hits = 1,
            Power = 1.0f
        };
    }

    private static Dictionary<string, int> BuildHeaderMap(List<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            var key = headers[i]?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            map[key] = i;
        }
        return map;
    }

    private static SkillData ParseSkill(List<string> row, Dictionary<string, int> headerMap)
    {
        string Get(string key)
        {
            if (!headerMap.TryGetValue(key, out var index))
            {
                return "";
            }
            if (index < 0 || index >= row.Count)
            {
                return "";
            }
            return row[index]?.Trim() ?? "";
        }

        var skill = new SkillData
        {
            SkillId = Get("SkillId"),
            Name = Get("Name"),
            TargetTeam = ParseEnum(Get("TargetTeam"), SkillTargetTeam.Enemy),
            TargetShape = ParseEnum(Get("TargetShape"), SkillTargetShape.Single),
            RangeStep = ParseInt(Get("Range"), 0),
            RadiusStep = ParseInt(Get("Radius"), 0),
            Hits = ParseInt(Get("Hits"), 1),
            Power = ParseFloat(Get("Power"), 1.0f),
            AccuracyBonus = ParseFloat(Get("AccuracyBonus"), 0f),
            Animation = Get("Animation"),
            BonusDamageMultiplier = ParseFloat(Get("BonusDamageMultiplier"), 1.0f),
            BonusHpMultiplier = ParseFloat(Get("BonusHpMultiplier"), 1.0f)
        };

        var traitText = Get("Traits");
        if (!string.IsNullOrWhiteSpace(traitText))
        {
            skill.Traits = ParseTraits(traitText);
        }

        if (skill.BonusDamageMultiplier > 1.0f)
        {
            skill.Traits |= SkillTrait.BonusDamage;
        }

        if (skill.BonusHpMultiplier > 1.0f)
        {
            skill.Traits |= SkillTrait.BonusHpMultiplier;
        }

        return skill;
    }

    private static SkillTrait ParseTraits(string text)
    {
        var traits = SkillTrait.None;
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var token = part.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (string.Equals(token, "bonus_damage", StringComparison.OrdinalIgnoreCase))
            {
                traits |= SkillTrait.BonusDamage;
            }
            else if (string.Equals(token, "ignore_armor", StringComparison.OrdinalIgnoreCase))
            {
                traits |= SkillTrait.IgnoreArmor;
            }
            else if (string.Equals(token, "bonus_hp_multiplier", StringComparison.OrdinalIgnoreCase))
            {
                traits |= SkillTrait.BonusHpMultiplier;
            }
        }

        return traits;
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (Enum.TryParse(value, true, out T result))
        {
            return result;
        }

        return fallback;
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
