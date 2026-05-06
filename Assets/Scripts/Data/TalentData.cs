using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Talent", fileName = "Talent_")]
    public class TalentData : ScriptableObject
    {
        public string talentName;
        [TextArea] public string description;
        public Sprite icon;

        [Tooltip("Talents are positive-only per GDD. Each talent can apply multiple stat modifiers.")]
        public List<StatModifierEntry> modifiers = new List<StatModifierEntry>();

        [Header("Stacking")]
        public bool stackable = true;
        public int maxStacks = 99;

        [Header("Duration")]
        [Tooltip("-1 = permanent; >0 = expires after that many rooms")]
        public int roomDuration = -1;
    }

    [System.Serializable]
    public struct StatModifierEntry
    {
        public StatType stat;
        public ModifierOp op;
        public float value;
    }
}
