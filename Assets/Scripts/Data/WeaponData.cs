using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Weapon", fileName = "Weapon_")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Classification")]
        public WeaponCategory category = WeaponCategory.Longsword;
        public WeaponRarity rarity = WeaponRarity.White;
        public DamageType damageType = DamageType.Physical;

        [Header("Base Stats")]
        public float baseDamage = 10f;
        public float attackSpeed = 1f;    // 每秒攻击次数
        public float attackRange = 1.5f;  // 攻击范围（米）
        public float aoeRadius = 0f;      // 普攻AOE半径（0=单体）

        [Header("Upgrade (升级)")]
        public int maxUpgradeLevel = 5;
        public float damagePerLevel = 3f;  // 每级提升基础伤害

        [Header("Skill (蓝/紫武器专属技能)")]
        public WeaponSkillData skill;

        [Header("Enchant (附魔，仅蓝/紫可用)")]
        public int maxEnchantLevel = 5;
        public float skillMultiplierPerEnchant = 0.1f;  // 每级附魔提升10%技能倍率

        public bool HasSkill => rarity >= WeaponRarity.Blue && skill != null;
        public bool CanEnchant => HasSkill;

        public static Color GetRarityColor(WeaponRarity r)
        {
            switch (r)
            {
                case WeaponRarity.White:  return Color.white;
                case WeaponRarity.Green:  return new Color(0.4f, 1f, 0.4f);
                case WeaponRarity.Blue:   return new Color(0.4f, 0.7f, 1f);
                case WeaponRarity.Purple: return new Color(0.8f, 0.4f, 1f);
                default: return Color.white;
            }
        }

        public static string GetRarityLabel(WeaponRarity r)
        {
            switch (r)
            {
                case WeaponRarity.White:  return "[白]";
                case WeaponRarity.Green:  return "[绿]";
                case WeaponRarity.Blue:   return "[蓝]";
                case WeaponRarity.Purple: return "[紫]";
                default: return "";
            }
        }
    }
}
