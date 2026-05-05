using Game.Data;
using UnityEngine;

namespace Game.Combat
{
    [RequireComponent(typeof(CharacterStats))]
    public class Health : MonoBehaviour, IDamageable
    {
        private CharacterStats _stats;
        private float _current;

        public float Current => _current;
        public float Max => _stats != null ? _stats.Get(StatType.MaxHP) : 0f;
        public float Ratio => Max > 0f ? _current / Max : 0f;

        public GameObject LastDamageSource { get; private set; }

        public System.Action<DamageInfo> OnDamaged;
        public System.Action OnDied;

        // 可选拦截器：在计算防御前修改伤害（用于盾牌减伤等机制）
        public System.Func<DamageInfo, DamageInfo> OnBeforeTakeDamage;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
            _stats.OnStatsChanged += ClampToMax;
            _current = Max;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (_current <= 0f) return;

            LastDamageSource = info.Source;
            if (OnBeforeTakeDamage != null) info = OnBeforeTakeDamage(info);

            float defense = _stats.Get(StatType.Defense);
            float dmg = info.Type == DamageType.True
                ? info.Amount
                : Mathf.Max(1f, info.Amount - defense);

            _current = Mathf.Max(0f, _current - dmg);
            // Pass actual dealt damage so subscribers (e.g. floating numbers) show correct values
            OnDamaged?.Invoke(new DamageInfo { Amount = dmg, Type = info.Type, IsCrit = info.IsCrit, Source = info.Source });

            if (_current <= 0f) OnDied?.Invoke();
        }

        public void Heal(float amount)
        {
            if (_current <= 0f) return;
            _current = Mathf.Min(Max, _current + amount);
        }

        private void ClampToMax()
        {
            _current = Mathf.Min(_current, Max);
        }
    }
}
