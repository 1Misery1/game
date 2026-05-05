using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 暗影刺客：保持距离潜行，冷却结束后瞬移至玩家身旁发动爆发攻击，随即隐退
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class ShadowAssassinAI : MonoBehaviour
    {
        public Transform target;

        [Header("Stalk Movement")]
        public float preferredMinDist = 5f;
        public float preferredMaxDist = 8f;

        [Header("Blink Strike")]
        public float blinkCooldown   = 5f;
        public float burstDamage     = 28f;
        public float retreatDistance = 6f;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private float _nextBlinkTime;

        private void Awake()
        {
            _rb    = GetComponent<Rigidbody2D>();
            _stats = GetComponent<CharacterStats>();
            _nextBlinkTime = Time.time + blinkCooldown;
        }

        private void Update()
        {
            if (target == null) return;
            if (Time.time >= _nextBlinkTime)
                DoBlink();
        }

        private void FixedUpdate()
        {
            if (target == null) return;
            float   dist  = Vector2.Distance(transform.position, target.position);
            Vector2 dir   = ((Vector2)target.position - _rb.position).normalized;
            float   speed = _stats.Get(StatType.MoveSpeed);

            if (dist > preferredMaxDist)
                _rb.MovePosition(_rb.position + dir * speed * Time.fixedDeltaTime);
            else if (dist < preferredMinDist)
                _rb.MovePosition(_rb.position - dir * speed * Time.fixedDeltaTime);
        }

        private void DoBlink()
        {
            _nextBlinkTime = Time.time + blinkCooldown;
            if (target == null) return;

            // 出现在玩家身旁
            Vector2 offset = Random.insideUnitCircle.normalized * 0.6f;
            transform.position = (Vector2)target.position + offset;

            // 爆发伤害
            target.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
            {
                Amount = burstDamage + _stats.Get(StatType.Attack),
                Type   = DamageType.Physical,
                Source = gameObject
            });

            // 立即隐退
            Vector2 retreatDir = ((Vector2)transform.position - (Vector2)target.position).normalized;
            if (retreatDir == Vector2.zero)
                retreatDir = Random.insideUnitCircle.normalized;
            transform.position = (Vector2)target.position + retreatDir * retreatDistance;
        }
    }
}
