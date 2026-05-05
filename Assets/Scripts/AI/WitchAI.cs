using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 女巫（精英）：
    //   - 保持距离，使用法杖进行魔法AOE攻击
    //   - 周期性召唤2只飞天蝙蝠
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class WitchAI : MonoBehaviour
    {
        public Transform target;

        [Header("Ranged Attack (法杖)")]
        public float preferredDistance = 6f;
        public float attackRange       = 8f;
        public float attackInterval    = 2.5f;
        public float attackDamage      = 18f;
        public float blastRadius       = 1.8f;

        [Header("Summon (召唤蝙蝠)")]
        public float summonCooldown = 8f;
        public int   summonCount    = 2;

        // 外部注入：召唤蝙蝠时需要的工厂委托
        // GameBootstrap会在生成女巫后设置此字段
        public System.Func<Vector3, GameObject> SpawnBatCallback;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private float _lastAttackTime;
        private float _lastSummonTime = -100f;

        private void Awake()
        {
            _rb    = GetComponent<Rigidbody2D>();
            _stats = GetComponent<CharacterStats>();
        }

        private void FixedUpdate()
        {
            if (target == null) return;
            float dist  = Vector2.Distance(transform.position, target.position);
            Vector2 dir = ((Vector2)target.position - _rb.position).normalized;
            float speed = _stats.Get(StatType.MoveSpeed);

            if (dist > preferredDistance + 1.5f)
                _rb.MovePosition(_rb.position + dir * speed * Time.fixedDeltaTime);
            else if (dist < preferredDistance - 1.5f)
                _rb.MovePosition(_rb.position - dir * speed * Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (target == null) return;

            // 召唤蝙蝠
            if (Time.time >= _lastSummonTime + summonCooldown)
                SummonBats();

            // 法杖攻击
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= attackRange && Time.time >= _lastAttackTime + attackInterval)
                CastBlast();
        }

        private void CastBlast()
        {
            _lastAttackTime = Time.time;
            float dmg   = attackDamage + _stats.Get(StatType.Attack);
            Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
            Vector2 blastCenter = (Vector2)transform.position + dir * Mathf.Min(attackRange, 5f);

            var cols = Physics2D.OverlapCircleAll(blastCenter, blastRadius);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = dmg,
                    Type   = DamageType.Magical,
                    Source = gameObject
                });
            }
        }

        private void SummonBats()
        {
            _lastSummonTime = Time.time;
            if (SpawnBatCallback == null) return;

            for (int i = 0; i < summonCount; i++)
            {
                Vector2 offset   = Random.insideUnitCircle.normalized * 1.5f;
                Vector3 spawnPos = transform.position + (Vector3)offset;
                SpawnBatCallback(spawnPos);
            }
        }
    }
}
