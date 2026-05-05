using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 腐败盾士：近战追击，周期性举盾（HP≤50%时也会触发），举盾时来自玩家方向的伤害减少80%
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class ShieldGuardAI : MonoBehaviour
    {
        public Transform target;
        public float attackRange       = 1.1f;
        public float attackInterval    = 1.5f;
        public float contactDamage     = 14f;
        public float shieldInterval    = 8f;    // 举盾周期
        public float shieldDuration    = 3f;    // 举盾持续时间
        public float shieldReduction   = 0.8f;  // 正面伤害减少比例

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private Health         _health;
        private float _lastAttackTime;
        private float _lastShieldTime  = -100f;
        private float _shieldUntil;
        private bool  _halfHpTriggered;

        public bool IsShielding => Time.time < _shieldUntil;

        private void Awake()
        {
            _rb     = GetComponent<Rigidbody2D>();
            _stats  = GetComponent<CharacterStats>();
            _health = GetComponent<Health>();

            // 拦截伤害：举盾时减少来自玩家正面的伤害
            _health.OnBeforeTakeDamage = InterceptDamage;
        }

        private void Update()
        {
            if (target == null) return;

            // 血量降至50%触发举盾
            if (!_halfHpTriggered && _health != null && _health.Current <= _health.Max * 0.5f)
            {
                _halfHpTriggered = true;
                RaiseShield();
            }

            // 周期性举盾
            if (!IsShielding && Time.time >= _lastShieldTime + shieldInterval)
                RaiseShield();

            // 近战攻击
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= attackRange && Time.time >= _lastAttackTime + attackInterval)
                DoAttack();
        }

        private void FixedUpdate()
        {
            if (target == null || IsShielding) return; // 举盾时停止移动，转向玩家
            float dist  = Vector2.Distance(transform.position, target.position);
            if (dist > attackRange)
            {
                Vector2 dir   = ((Vector2)target.position - _rb.position).normalized;
                float   speed = _stats.Get(StatType.MoveSpeed);
                _rb.MovePosition(_rb.position + dir * speed * Time.fixedDeltaTime);
            }
        }

        private void RaiseShield()
        {
            _shieldUntil   = Time.time + shieldDuration;
            _lastShieldTime = Time.time;
        }

        private void DoAttack()
        {
            _lastAttackTime = Time.time;
            target.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
            {
                Amount = contactDamage + _stats.Get(StatType.Attack),
                Type   = DamageType.Physical,
                Source = gameObject
            });
        }

        private DamageInfo InterceptDamage(DamageInfo info)
        {
            if (!IsShielding || target == null || info.Source == null) return info;

            // 盾牌朝向玩家（盾士面朝玩家方向）
            Vector2 toTarget  = ((Vector2)target.position - (Vector2)transform.position).normalized;
            Vector2 fromSrc   = ((Vector2)info.Source.transform.position - (Vector2)transform.position).normalized;

            // dot > 0.5 表示伤害来自盾牌正面，减伤生效
            if (Vector2.Dot(toTarget, fromSrc) > 0.5f)
                info.Amount *= (1f - shieldReduction);

            return info;
        }
    }
}
