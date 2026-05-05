using Game.Combat;
using Game.Data;
using Game.Player;
using UnityEngine;

namespace Game.AI
{
    // 霜魂巫妖（第二层Boss）
    //
    // 基础攻击：冰霜光线，射程8，20魔法伤害，CD 3s
    //
    // 技能1·冰霜新星 (FrostNova)：
    //   以自身为圆心AOE，r=3.5，20伤害 + 打断玩家0.5s
    //   CD：5s → Phase2 3.5s
    //
    // 技能2·冰锥齐射 (IceVolley)：
    //   向玩家方向发射扇形射线，Phase1:3条/±15°，Phase2:5条/±30°
    //   每条15魔法伤害，射程10
    //   CD：4s → Phase2 2.8s
    //
    // 移动：维持与玩家4~7格距离（远程保持型）
    // Phase2触发：HP≤50%，技能CD×0.7
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class FrostLichAI : MonoBehaviour
    {
        public Transform target;

        [Header("Basic Attack")]
        public float attackRange    = 8f;
        public float attackInterval = 3f;
        public float attackDamage   = 20f;

        [Header("Skill 1 - Frost Nova")]
        public float novaCooldown = 5f;
        public float novaRadius   = 3.5f;
        public float novaDamage   = 20f;
        public float novaStun     = 0.5f;

        [Header("Skill 2 - Ice Volley")]
        public float volleyCooldown  = 4f;
        public float volleyDamage    = 15f;
        public float volleyRange     = 10f;
        public int   volleyCount_P1  = 3;
        public int   volleyCount_P2  = 5;

        [Header("Movement")]
        public float preferredMinDist = 4f;
        public float preferredMaxDist = 7f;

        [Header("Phase 2 (≤50% HP)")]
        public float phase2CdMultiplier = 0.7f;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private Health         _health;

        private float _lastAttackTime  = -100f;
        private float _lastNovaTime    = -100f;
        private float _lastVolleyTime  = -100f;
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

            float novaCd   = _isPhase2 ? novaCooldown   * phase2CdMultiplier : novaCooldown;
            float volleyCd = _isPhase2 ? volleyCooldown * phase2CdMultiplier : volleyCooldown;

            if (Time.time >= _lastNovaTime   + novaCd)   CastFrostNova();
            if (Time.time >= _lastVolleyTime + volleyCd) CastIceVolley();

            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= attackRange && Time.time >= _lastAttackTime + attackInterval)
                DoRangedAttack();
        }

        private void FixedUpdate()
        {
            if (target == null) return;
            float   dist     = Vector2.Distance(transform.position, target.position);
            float   speed    = _stats.Get(StatType.MoveSpeed);
            Vector2 toTarget = ((Vector2)target.position - _rb.position).normalized;

            if (dist < preferredMinDist)
                _rb.MovePosition(_rb.position - toTarget * speed * Time.fixedDeltaTime);
            else if (dist > preferredMaxDist)
                _rb.MovePosition(_rb.position + toTarget * speed * Time.fixedDeltaTime);
        }

        private void CheckPhase2()
        {
            if (_phase2Triggered || _health == null) return;
            if (_health.Current <= _health.Max * 0.5f)
            {
                _phase2Triggered = true;
                _isPhase2        = true;
            }
        }

        // 冰霜光线：命中第一个有效目标
        private void DoRangedAttack()
        {
            _lastAttackTime = Time.time;
            if (target == null) return;
            Vector2 dir  = ((Vector2)target.position - (Vector2)transform.position).normalized;
            var     hits = Physics2D.RaycastAll(transform.position, dir, attackRange);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;
                var d = hit.collider.GetComponent<IDamageable>();
                if (d == null) continue;
                d.TakeDamage(new DamageInfo { Amount = attackDamage, Type = DamageType.Magical, Source = gameObject });
                break;
            }
        }

        // 冰霜新星：AOE + 打断玩家
        private void CastFrostNova()
        {
            _lastNovaTime = Time.time;
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, novaRadius))
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = novaDamage, Type = DamageType.Magical, Source = gameObject
                });
                col.GetComponent<PlayerController>()?.ApplyStun(novaStun);
            }
        }

        // 冰锥齐射：扇形多条射线
        private void CastIceVolley()
        {
            _lastVolleyTime = Time.time;
            if (target == null) return;

            int     count    = _isPhase2 ? volleyCount_P2 : volleyCount_P1;
            Vector2 baseDir  = ((Vector2)target.position - (Vector2)transform.position).normalized;
            float   step     = 15f;                              // 每条间隔15°
            float   start    = -step * (count - 1) * 0.5f;      // 对称展开

            for (int i = 0; i < count; i++)
            {
                Vector2 dir  = Rotate(baseDir, start + i * step);
                var     hits = Physics2D.RaycastAll(transform.position, dir, volleyRange);
                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject == gameObject) continue;
                    var d = hit.collider.GetComponent<IDamageable>();
                    if (d == null) continue;
                    d.TakeDamage(new DamageInfo { Amount = volleyDamage, Type = DamageType.Magical, Source = gameObject });
                    break;
                }
            }
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
