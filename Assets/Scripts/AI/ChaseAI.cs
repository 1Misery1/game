using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class ChaseAI : MonoBehaviour
    {
        public Transform target;
        public float stoppingDistance = 0.9f;
        public float attackInterval = 1.2f;
        public float contactDamage = 10f;

        private Rigidbody2D _rb;
        private CharacterStats _stats;
        private float _lastAttackTime;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _stats = GetComponent<CharacterStats>();
        }

        private void FixedUpdate()
        {
            if (target == null) return;

            Vector2 delta = (Vector2)target.position - _rb.position;
            float dist = delta.magnitude;
            if (dist < 0.001f) return;

            if (dist > stoppingDistance)
            {
                float speed = _stats.Get(StatType.MoveSpeed);
                Vector2 step = delta / dist * speed * Time.fixedDeltaTime;
                _rb.MovePosition(_rb.position + step);
            }
            else if (Time.time >= _lastAttackTime + attackInterval)
            {
                var dmg = target.GetComponent<IDamageable>();
                if (dmg != null)
                {
                    dmg.TakeDamage(new DamageInfo
                    {
                        Amount = contactDamage,
                        Type = DamageType.Physical,
                        Source = gameObject
                    });
                }
                _lastAttackTime = Time.time;
            }
        }
    }
}
