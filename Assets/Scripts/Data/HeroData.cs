using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Hero", fileName = "Hero_")]
    public class HeroData : ScriptableObject
    {
        public string heroName;
        [TextArea] public string description;
        public Sprite portrait;
        public GameObject prefab;
        public Color tintColor = Color.white;

        [Header("Base Stats")]
        public float baseMaxHP = 100f;
        public float baseAttack = 10f;
        public float baseDefense = 0f;
        public float baseMoveSpeed = 5f;
        public float baseAttackSpeed = 1f;

        [Header("Skills")]
        public ActiveSkillData activeSkill;
        public PassiveTalentData passiveTalent;
        public HeroSkillType heroSkillType = HeroSkillType.None;
        public float heroSkillCooldown = 8f;
        public string heroSkillName;
        public HeroPassiveType heroPassiveType = HeroPassiveType.None;
        public string heroPassiveName;

        [Header("Meta Progression")]
        public bool unlockedByDefault = false;
        public int unlockCost = 100;
    }
}
