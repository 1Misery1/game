namespace Game.Data
{
    // 武器实例：持有WeaponData模板 + 当前升级/附魔等级
    public class WeaponInstance
    {
        public readonly WeaponData Data;
        public int UpgradeLevel { get; private set; }   // 0~maxUpgradeLevel
        public int EnchantLevel { get; private set; }   // 0~maxEnchantLevel，仅蓝/紫可用

        public WeaponInstance(WeaponData data)
        {
            Data = data;
        }

        // 实际基础伤害（含升级加成）
        public float EffectiveDamage => Data.baseDamage + Data.damagePerLevel * UpgradeLevel;

        // 实际技能倍率（含附魔加成）
        public float EffectiveSkillMultiplier =>
            Data.HasSkill
                ? Data.skill.damageMultiplier + Data.skillMultiplierPerEnchant * EnchantLevel
                : 0f;

        public bool TryUpgrade()
        {
            if (UpgradeLevel >= Data.maxUpgradeLevel) return false;
            UpgradeLevel++;
            return true;
        }

        public bool TryEnchant()
        {
            if (!Data.CanEnchant) return false;
            if (EnchantLevel >= Data.maxEnchantLevel) return false;
            EnchantLevel++;
            return true;
        }

        public string ShortName
        {
            get
            {
                string name = WeaponData.GetRarityLabel(Data.rarity) + Data.weaponName;
                if (UpgradeLevel > 0) name += $" +{UpgradeLevel}";
                if (EnchantLevel > 0) name += $" ★{EnchantLevel}";
                return name;
            }
        }

        public string CategoryLabel
        {
            get
            {
                switch (Data.category)
                {
                    case WeaponCategory.Dagger:     return "匕首";
                    case WeaponCategory.Longsword:  return "长剑";
                    case WeaponCategory.Greatsword: return "双手剑";
                    case WeaponCategory.Bow:        return "弓";
                    case WeaponCategory.Staff:      return "法杖";
                    default: return "";
                }
            }
        }
    }
}
