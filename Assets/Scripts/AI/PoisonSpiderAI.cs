using System.Collections;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 毒蜘蛛：快速追击，接触时附加毒素DoT
    // 死亡时留下毒液水坑（由 GameBootstrap 的 AttachSpecialDeathEffect 处理）
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class PoisonSpiderAI : MonoBehaviour
    {
        public Transform target;
        public float stoppingDistance = 0.7f;
        public float attackInterval   = 1.2f;
        public float contactDamage    = 10f;

        [Header("Poison DoT")]
        public float poisonTickDamage  = 5f;
        public int   poisonTicks       = 4;
        public float poisonTickInterval = 0.6f;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private float _lastAttackTime;

        private void Awake()
        {
            _rb    = GetComponent<Rigidbody2D>();
            _stats = GetComponent<CharacterStats>();
        }

        private void FixedUpdate()
        {
            if (target == null) return;
            Vector2 delta = (Vector2)target.position - _rb.position;
            float   dist  = delta.magnitude;
            if (dist < 0.001f) return;

            if (dist > stoppingDistance)
            {
                _rb.MovePosition(_rb.position + delta / dist * _stats.Get(StatType.MoveSpeed) * Time.fixedDeltaTime);
            }
            else if (Time.time >= _lastAttackTime + attackInterval)
            {
                _lastAttackTime = Time.time;
                var d = target.GetComponent<IDamageable>();
                if (d == null) return;
                d.TakeDamage(new DamageInfo { Amount = contactDamage, Type = DamageType.Physical, Source = gameObject });
                StartCoroutine(ApplyPoisonDoT(d));
            }
        }

        private IEnumerator ApplyPoisonDoT(IDamageable damageable)
        {
            var comp = damageable as Component;
            for (int i = 0; i < poisonTicks; i++)
            {
                yield return new WaitForSeconds(poisonTickInterval);
                if (comp == null) yield break;
                damageable.TakeDamage(new DamageInfo
                {
                    Amount = poisonTickDamage, Type = DamageType.True, Source = gameObject
                });
            }
        }
    }
}
