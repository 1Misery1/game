using System.Collections.Generic;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 腐败士官（精英）：
    //   - 近战使用双手剑大范围AOE攻击
    //   - 周期性释放光环，强化周围小怪（+50% MaxHP, +30% AttackSpeed），持续8s
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class CommanderAI : MonoBehaviour
    {
        public Transform target;

        [Header("Melee (双手剑)")]
        public float attackRange    = 2.2f;   // 宽范围挥击
        public float attackInterval = 2.0f;
        public float attackDamage   = 20f;

        [Header("Aura (光环)")]
        public float auraRadius    = 8f;
        public float auraCooldown  = 10f;
        public float auraDuration  = 8f;
        public float auraHpBonus   = 0.5f;   // +50% MaxHP
        public float auraAtkBonus  = 0.3f;   // +30% AttackSpeed

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private float _lastAttackTime;
        private float _lastAuraTime = -100f;

        // 记录当前光环buff了哪些敌人，到期后移除
        private readonly List<CharacterStats> _buffedMinions = new List<CharacterStats>();
        private float _auraExpiresAt;

        private void Awake()
        {
            _rb    = GetComponent<Rigidbody2D>();
            _stats = GetComponent<CharacterStats>();
        }

        private void Update()
        {
            if (target == null) return;

            // 光环到期清除buff
            if (_buffedMinions.Count > 0 && Time.time >= _auraExpiresAt)
                ClearAuraBuff();

            // 周期性光环
            if (Time.time >= _lastAuraTime + auraCooldown)
                CastAura();

            // 近战攻击（大范围挥击）
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= attackRange && Time.time >= _lastAttackTime + attackInterval)
                DoSwing();
        }

        private void FixedUpdate()
        {
            if (target == null) return;
            float dist  = Vector2.Distance(transform.position, target.position);
            if (dist > attackRange * 0.8f)
            {
                Vector2 dir   = ((Vector2)target.position - _rb.position).normalized;
                float   speed = _stats.Get(StatType.MoveSpeed);
                _rb.MovePosition(_rb.position + dir * speed * Time.fixedDeltaTime);
            }
        }

        private void DoSwing()
        {
            _lastAttackTime = Time.time;
            float dmg  = attackDamage + _stats.Get(StatType.Attack);
            var   cols = Physics2D.OverlapCircleAll(transform.position, attackRange);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = dmg,
                    Type   = DamageType.Physical,
                    Source = gameObject
                });
            }
        }

        private void CastAura()
        {
            _lastAuraTime  = Time.time;
            _auraExpiresAt = Time.time + auraDuration;
            ClearAuraBuff();

            var cols = Physics2D.OverlapCircleAll(transform.position, auraRadius);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                var tag = col.GetComponent<EnemyTag>();
                if (tag == null || !tag.IsAuraTarget) continue;

                var stats = col.GetComponent<CharacterStats>();
                if (stats == null) continue;

                stats.AddModifier(new StatModifier(StatType.MaxHP,       ModifierOp.PercentMul, auraHpBonus,  gameObject));
                stats.AddModifier(new StatModifier(StatType.AttackSpeed, ModifierOp.PercentMul, auraAtkBonus, gameObject));
                _buffedMinions.Add(stats);
            }
        }

        private void ClearAuraBuff()
        {
            foreach (var stats in _buffedMinions)
            {
                if (stats != null)
                    stats.RemoveModifiersFrom(gameObject);
            }
            _buffedMinions.Clear();
        }

        private void OnDestroy() => ClearAuraBuff();
    }
}
