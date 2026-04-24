using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(menuName = "Game/Buff", fileName = "Buff_")]
    public class BuffData : ScriptableObject
    {
        public string buffName;
        [TextArea] public string description;
        public Sprite icon;

        [Tooltip("Buffs can be positive or negative per GDD (e.g. +DMG OR -MaxHP).")]
        public bool isNegative = false;

        public List<StatModifierEntry> modifiers = new List<StatModifierEntry>();

        [Header("Duration (0 = permanent for this run)")]
        public float durationSeconds = 0f;
    }
}
