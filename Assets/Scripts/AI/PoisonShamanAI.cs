using System.Collections;
using System.Collections.Generic;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 毒蛇祭司（精英）：
    //   - 保持距离，使用毒素光线攻击（物理伤害+毒素DoT）
    //   - 周期性强化周围毒蜘蛛（+50% ATK，+1.5 SPD），持续5s
    //   - 周期性在玩家位置投放毒液水坑（委托 GameBootstrap 生成）
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class PoisonShamanAI : MonoBehaviour
    {
        public Transform target;

        [Header("Movement")]
        public float preferredMinDist = 5f;
        public float preferredMaxDist = 8f;

        [Header("Poison Bolt")]
        public float boltCooldown     = 3f;
        public float boltRange        = 8f;
        public float boltDamage       = 18f;
        public float poisonTickDamage = 4f;
        public int   poisonTicks      = 3;
        public float poisonTickInterval = 0.7f;

        [Header("Spider Aura")]
        public float spiderBuffCooldown = 6f;
        public float spiderAuraRadius   = 8f;
        public float spiderBuffDuration = 5f;

        [Header("Poison Puddle")]
        public float puddleCooldown = 8f;

        // GameBootstrap注入：在指定位置生成毒液水坑
        public System.Action<Vector3> SpawnPoisonPuddleCallback;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;

        private float _lastBoltTime       = -100f;
        private float _lastSpiderBuffTime = -100f;
        private float _lastPuddleTime     = -100f;
        private float _spiderBuffExpiresAt;

        private readonly List<CharacterStats> _buffedSpiders = new List<CharacterStats>();

        private void Awake()
        {
            _rb    = GetComponent<Rigidbody2D>();
            _stats = GetComponent<CharacterStats>();
        }

        private void Update()
        {
            if (target == null) return;

            // 蜘蛛buff到期
            if (_buffedSpiders.Count > 0 && Time.time >= _spiderBuffExpiresAt)
                ClearSpiderBuff();

            if (Time.time >= _lastBoltTime       + boltCooldown)   CastPoisonBolt();
            if (Time.time >= _lastSpiderBuffTime + spiderBuffCooldown) CastSpiderBuff();
            if (Time.time >= _lastPuddleTime     + puddleCooldown)  DropPoisonPuddle();
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

        // 毒素光线：命中后附加真实伤害DoT
        private void CastPoisonBolt()
        {
            _lastBoltTime = Time.time;
            if (target == null) return;

            Vector2 dir  = ((Vector2)target.position - (Vector2)transform.position).normalized;
            var     hits = Physics2D.RaycastAll(transform.position, dir, boltRange);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;
                var d = hit.collider.GetComponent<IDamageable>();
                if (d == null) continue;
                d.TakeDamage(new DamageInfo { Amount = boltDamage, Type = DamageType.Physical, Source = gameObject });
                StartCoroutine(ApplyPoisonDoT(d));
                break;
            }
        }

        // 强化附近毒蜘蛛
        private void CastSpiderBuff()
        {
            _lastSpiderBuffTime  = Time.time;
            _spiderBuffExpiresAt = Time.time + spiderBuffDuration;
            ClearSpiderBuff();

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, spiderAuraRadius))
            {
                var tag = col.GetComponent<EnemyTag>();
                if (tag == null || tag.type != EnemyType.PoisonSpider) continue;
                var stats = col.GetComponent<CharacterStats>();
                if (stats == null) continue;
                stats.AddModifier(new StatModifier(StatType.Attack,    ModifierOp.PercentMul, 0.5f,  gameObject));
                stats.AddModifier(new StatModifier(StatType.MoveSpeed, ModifierOp.Flat,       1.5f,  gameObject));
                _buffedSpiders.Add(stats);
            }
        }

        private void ClearSpiderBuff()
        {
            foreach (var stats in _buffedSpiders)
                if (stats != null) stats.RemoveModifiersFrom(gameObject);
            _buffedSpiders.Clear();
        }

        // 在玩家位置投放毒液水坑
        private void DropPoisonPuddle()
        {
            _lastPuddleTime = Time.time;
            if (target == null || SpawnPoisonPuddleCallback == null) return;
            SpawnPoisonPuddleCallback(target.position);
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

        private void OnDestroy() => ClearSpiderBuff();
    }
}
