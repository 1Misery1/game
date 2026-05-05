using System.Collections;
using Game.Combat;
using Game.Data;
using Game.Player;
using UnityEngine;

namespace Game.AI
{
    // 混沌领主（第三层Boss）
    //
    // 基础攻击：近战横扫，r=2.5，45物理伤害 + 击退，CD 1.5s
    //
    // 技能1·混沌爆发 (ChaosBurst)：
    //   3次脉冲AOE，r=5，每次30魔法伤害 + 击退，0.5s间隔
    //   CD：7s → Phase2 4.5s
    //
    // 技能2·召唤军团 (SummonLegion)：
    //   在随机位置召唤小怪，Phase1:2只，Phase2:3只
    //   CD：12s → Phase2 8s
    //   通过 SpawnMinionCallback 委托实际生成
    //
    // Phase2触发：HP≤40%，移速+0.8，技能CD缩短
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class ChaosLordAI : MonoBehaviour
    {
        public Transform target;

        [Header("Basic Attack")]
        public float attackRange    = 2.5f;
        public float attackInterval = 1.5f;
        public float attackDamage   = 45f;
        public float attackKnockback = 10f;

        [Header("Skill 1 - Chaos Burst")]
        public float burstCooldown  = 7f;
        public float burstRadius    = 5f;
        public float burstDamage    = 30f;
        public float burstKnockback = 14f;
        public int   burstPulses    = 3;

        [Header("Skill 2 - Summon Legion")]
        public float summonCooldown  = 12f;
        public int   summonCount_P1  = 2;
        public int   summonCount_P2  = 3;

        [Header("Phase 2 (≤40% HP)")]
        public float phase2SpeedBonus    = 0.8f;
        public float phase2BurstCooldown = 4.5f;
        public float phase2SummonCooldown = 8f;

        // 外部注入：在指定位置生成一个随机小怪
        public System.Func<Vector3, GameObject> SpawnMinionCallback;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private Health         _health;

        private float _lastAttackTime = -100f;
        private float _lastBurstTime  = -100f;
        private float _lastSummonTime = -100f;
        private bool  _burstBusy;
        private bool  _phase2Triggered;
        private bool  _isPhase2;

        private void Awake()
        {
            _rb     = GetComponent<Rigidbody2D>();
            _stats  = GetComponent<CharacterStats>();
            _health = GetComponent<Health>();
        }

        private void Update()
        {
            if (target == null) return;
            CheckPhase2();

            float burstCd  = _isPhase2 ? phase2BurstCooldown  : burstCooldown;
            float summonCd = _isPhase2 ? phase2SummonCooldown  : summonCooldown;

            if (!_burstBusy && Time.time >= _lastBurstTime + burstCd)
                StartCoroutine(CastChaosBurst());

            if (Time.time >= _lastSummonTime + summonCd)
                CastSummonLegion();

            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= attackRange && Time.time >= _lastAttackTime + attackInterval)
                DoMeleeSwipe();
        }

        private void FixedUpdate()
        {
            if (target == null || _burstBusy) return;
            float dist  = Vector2.Distance(transform.position, target.position);
            if (dist > attackRange)
            {
                Vector2 dir   = ((Vector2)target.position - _rb.position).normalized;
                float   speed = _stats.Get(StatType.MoveSpeed);
                _rb.MovePosition(_rb.position + dir * speed * Time.fixedDeltaTime);
            }
        }

        private void CheckPhase2()
        {
            if (_phase2Triggered || _health == null) return;
            if (_health.Current <= _health.Max * 0.4f)
            {
                _phase2Triggered = true;
                _isPhase2        = true;
                _stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierOp.Flat, phase2SpeedBonus, "phase2"));
            }
        }

        // 近战横扫：AOE + 击退
        private void DoMeleeSwipe()
        {
            _lastAttackTime = Time.time;
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, attackRange))
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = attackDamage + _stats.Get(StatType.Attack),
                    Type   = DamageType.Physical, Source = gameObject
                });
                ApplyKnockback(col, attackKnockback);
            }
        }

        // 混沌爆发：连续多次脉冲AOE
        private IEnumerator CastChaosBurst()
        {
            _burstBusy    = true;
            _lastBurstTime = Time.time;
            for (int i = 0; i < burstPulses; i++)
            {
                DoBurstPulse();
                if (i < burstPulses - 1)
                    yield return new WaitForSeconds(0.5f);
            }
            _burstBusy = false;
        }

        private void DoBurstPulse()
        {
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, burstRadius))
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = burstDamage, Type = DamageType.Magical, Source = gameObject
                });
                ApplyKnockback(col, burstKnockback);
                col.GetComponent<PlayerController>()?.ApplyStun(0.3f);
            }
        }

        // 召唤军团：在随机位置生成小怪
        private void CastSummonLegion()
        {
            _lastSummonTime = Time.time;
            if (SpawnMinionCallback == null) return;
            int count = _isPhase2 ? summonCount_P2 : summonCount_P1;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f);
                SpawnMinionCallback(transform.position + (Vector3)offset);
            }
        }

        private void ApplyKnockback(Collider2D col, float force)
        {
            var rb = col.GetComponent<Rigidbody2D>();
            if (rb == null || col.gameObject == gameObject) return;
            Vector2 dir = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;
            rb.AddForce(dir * force, ForceMode2D.Impulse);
        }
    }
}
