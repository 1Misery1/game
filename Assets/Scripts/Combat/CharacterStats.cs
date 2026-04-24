using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Combat
{
    public class CharacterStats : MonoBehaviour
    {
        [SerializeField] private float baseMaxHP = 100f;
        [SerializeField] private float baseAttack = 10f;
        [SerializeField] private float baseDefense = 0f;
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private float baseAttackSpeed = 1f;

        private readonly Dictionary<StatType, float> _baseValues = new Dictionary<StatType, float>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private readonly Dictionary<StatType, float> _cache = new Dictionary<StatType, float>();
        private bool _dirty = true;

        public System.Action OnStatsChanged;

        private void Awake()
        {
            _baseValues[StatType.MaxHP] = baseMaxHP;
            _baseValues[StatType.Attack] = baseAttack;
            _baseValues[StatType.Defense] = baseDefense;
            _baseValues[StatType.MoveSpeed] = baseMoveSpeed;
            _baseValues[StatType.AttackSpeed] = baseAttackSpeed;
            _baseValues[StatType.CritRate] = 0.05f;
            _baseValues[StatType.CritDamage] = 1.5f;
            _baseValues[StatType.SkillPower] = 1f;
            _baseValues[StatType.CooldownReduction] = 0f;
            _baseValues[StatType.CoinGain] = 1f;
        }

        public void SetBase(StatType stat, float value)
        {
            _baseValues[stat] = value;
            _dirty = true;
        }

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            _dirty = true;
            OnStatsChanged?.Invoke();
        }

        public void RemoveModifiersFrom(object source)
        {
            int removed = _modifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
            if (removed > 0)
            {
                _dirty = true;
                OnStatsChanged?.Invoke();
            }
        }

        public float Get(StatType stat)
        {
            if (_dirty) Recalculate();
            return _cache.TryGetValue(stat, out var v) ? v : 0f;
        }

        private void Recalculate()
        {
            _cache.Clear();
            foreach (var kv in _baseValues)
            {
                var stat = kv.Key;
                float flat = kv.Value;
                float pctAdd = 0f;
                float pctMul = 1f;

                for (int i = 0; i < _modifiers.Count; i++)
                {
                    var m = _modifiers[i];
                    if (m.Stat != stat) continue;
                    switch (m.Op)
                    {
                        case ModifierOp.Flat: flat += m.Value; break;
                        case ModifierOp.PercentAdd: pctAdd += m.Value; break;
                        case ModifierOp.PercentMul: pctMul *= (1f + m.Value); break;
                    }
                }

                _cache[stat] = flat * (1f + pctAdd) * pctMul;
            }
            _dirty = false;
        }
    }
}
