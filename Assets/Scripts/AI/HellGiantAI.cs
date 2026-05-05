using System.Collections;
using Game.Combat;
using Game.Data;
using Game.Player;
using UnityEngine;

namespace Game.AI
{
    // 地狱巨人（Boss）
    //
    // 基础攻击：缓慢近战，2.0s间隔，35伤害
    //
    // 技能1·岩浆投掷 (LavaThrow)：
    //   Phase1 → 2团岩浆，Phase2 → 3团
    //   每团岩浆：半径2.5，持续5s，8伤害/s（真实伤害）
    //   冷却：6s → Phase2 4s
    //
    // 技能2·重踏 (GroundSlam)：
    //   Phase1 → 1次，Phase2 → 连续2次（间隔1.5s）
    //   每次：半径4，30伤害 + 击退 + 打断玩家施法0.8s
    //   冷却：10s → Phase2 7s
    //
    // Phase2触发：HP ≤ 50%
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class HellGiantAI : MonoBehaviour
    {
        public Transform target;

        [Header("Basic Attack")]
        public float attackRange    = 2.0f;
        public float attackInterval = 2.0f;
        public float attackDamage   = 35f;

        [Header("Skill 1 - Lava Throw")]
        public float lavaCooldown      = 6f;
        public float lavaDamagePerSec  = 8f;
        public float lavaLifetime      = 5f;
        public float lavaRadius        = 2.5f;
        public int   lavaCount_P1      = 2;
        public int   lavaCount_P2      = 3;

        [Header("Skill 2 - Ground Slam")]
        public float slamCooldown      = 10f;
        public float slamRadius        = 4f;
        public float slamDamage        = 30f;
        public float slamKnockback     = 12f;
        public float slamStunDuration  = 0.8f;

        [Header("Phase 2 (≤50% HP)")]
        public float phase2SpeedBonus  = 0.5f;

        // 外部注入：召唤岩浆池的工厂委托
        public System.Func<Vector3, float, float, float, GameObject> SpawnLavaCallback;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private Health         _health;
        private float _lastAttackTime;
        private float _lastLavaTime   = -100f;
        private float _lastSlamTime   = -100f;
        private bool  _isPhase2;
        private bool  _phase2Triggered;
        private bool  _slamBusy;

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

            float lavaCd = _isPhase2 ? lavaCooldown * 0.65f : lavaCooldown;
            float slamCd = _isPhase2 ? slamCooldown * 0.7f  : slamCooldown;

            if (Time.time >= _lastLavaTime + lavaCd)
                CastLavaThrow();

            if (!_slamBusy && Time.time >= _lastSlamTime + slamCd)
                StartCoroutine(CastGroundSlam());

            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= attackRange && Time.time >= _lastAttackTime + attackInterval)
                DoMelee();
        }

        private void FixedUpdate()
        {
            if (target == null || _slamBusy) return;
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
            if (_phase2Triggered) return;
            if (_health != null && _health.Current <= _health.Max * 0.5f)
            {
                _phase2Triggered = true;
                _isPhase2        = true;
                _stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierOp.Flat, phase2SpeedBonus, "phase2"));
            }
        }

        private void DoMelee()
        {
            _lastAttackTime = Time.time;
            target.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
            {
                Amount = attackDamage + _stats.Get(StatType.Attack),
                Type   = DamageType.Physical,
                Source = gameObject
            });
        }

        private void CastLavaThrow()
        {
            _lastLavaTime = Time.time;
            if (SpawnLavaCallback == null || target == null) return;

            int count = _isPhase2 ? lavaCount_P2 : lavaCount_P1;
            for (int i = 0; i < count; i++)
            {
                // 在玩家周围随机位置生成岩浆池
                Vector2 offset   = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4f);
                Vector3 pos      = target.position + (Vector3)offset;
                SpawnLavaCallback(pos, lavaDamagePerSec, lavaLifetime, lavaRadius);
            }
        }

        private IEnumerator CastGroundSlam()
        {
            _slamBusy     = true;
            _lastSlamTime = Time.time;

            int slamCount = _isPhase2 ? 2 : 1;
            for (int i = 0; i < slamCount; i++)
            {
                DoSingleSlam();
                if (i < slamCount - 1)
                    yield return new WaitForSeconds(1.5f);
            }

            _slamBusy = false;
        }

        private void DoSingleSlam()
        {
            // AOE伤害 + 击退 + 打断
            var cols = Physics2D.OverlapCircleAll(transform.position, slamRadius);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                var d = col.GetComponent<IDamageable>();
                if (d != null)
                {
                    d.TakeDamage(new DamageInfo
                    {
                        Amount = slamDamage,
                        Type   = DamageType.Physical,
                        Source = gameObject
                    });
                }

                // 击退 + 打断玩家
                var rb = col.GetComponent<Rigidbody2D>();
                if (rb != null && col.gameObject != gameObject)
                {
                    Vector2 knockDir = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;
                    rb.AddForce(knockDir * slamKnockback, ForceMode2D.Impulse);
                }

                // 打断玩家施法
                var pc = col.GetComponent<PlayerController>();
                if (pc != null) pc.ApplyStun(slamStunDuration);
            }
        }
    }
}
