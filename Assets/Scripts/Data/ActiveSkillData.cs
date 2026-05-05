using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Skills/Active Skill", fileName = "ActiveSkill_")]
    public class ActiveSkillData : ScriptableObject
    {
        public string skillName;
        [TextArea] public string description;
        public Sprite icon;
        public float cooldown = 5f;
        public float damageMultiplier = 2f;
        public GameObject vfxPrefab;
    }

    [CreateAssetMenu(menuName = "Game/Skills/Passive Talent", fileName = "Passive_")]
    public class PassiveTalentData : ScriptableObject
    {
        public string talentName;
        [TextArea] public string description;
        public Sprite icon;
        public System.Collections.Generic.List<StatModifierEntry> modifiers =
            new System.Collections.Generic.List<StatModifierEntry>();
    }

    [CreateAssetMenu(menuName = "Game/Skills/Weapon Skill", fileName = "WeaponSkill_")]
    public class WeaponSkillData : ScriptableObject
    {
        public string skillName;
        [TextArea] public string description;
        public Sprite icon;

        public WeaponSkillType skillType = WeaponSkillType.None;

        [Header("Cooldown & Damage")]
        public float cooldown = 3f;
        public float damageMultiplier = 1.5f;  // 技能伤害倍率（相对基础伤害）

        [Header("Skill Parameters")]
        public float skillRadius = 2.5f;   // AOE技能的爆炸半径
        public float skillRange = 8f;      // 技能射程
        public int skillHitCount = 1;      // 多段技能的段数

        public GameObject vfxPrefab;
    }
}
