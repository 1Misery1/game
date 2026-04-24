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
        public float cooldown = 3f;
        public float damageMultiplier = 1.5f;
        public GameObject vfxPrefab;
    }
}
