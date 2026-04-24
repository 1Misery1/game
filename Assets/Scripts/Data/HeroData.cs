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

        [Header("Base Stats")]
        public float baseMaxHP = 100f;
        public float baseAttack = 10f;
        public float baseDefense = 0f;
        public float baseMoveSpeed = 5f;
        public float baseAttackSpeed = 1f;

        [Header("Skills")]
        public ActiveSkillData activeSkill;
        public PassiveTalentData passiveTalent;

        [Header("Meta Progression")]
        public bool unlockedByDefault = false;
        public int unlockCost = 100;
    }
}
