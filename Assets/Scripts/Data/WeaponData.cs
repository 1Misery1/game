using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Weapon", fileName = "Weapon_")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName;
        [TextArea] public string description;
        public Sprite icon;
        public GameObject projectilePrefab;

        [Header("Classification")]
        public WeaponRarity rarity = WeaponRarity.Common;
        public DamageType damageType = DamageType.Physical;

        [Header("Stats")]
        public float baseDamage = 10f;
        public float attackSpeed = 1f;
        public float range = 5f;
        public float areaRadius = 0f;

        [Header("Unique Skill")]
        public WeaponSkillData uniqueSkill;

        [Header("Upgrade")]
        public int maxUpgradeLevel = 5;
        public float damagePerLevel = 2f;
        public float critPerLevel = 0.02f;
        public float cdReductionPerLevel = 0.05f;
    }
}
