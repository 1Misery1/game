using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 死灵术士（精英）：
    //   - 保持距离，灵魂汲取（远程魔法+自身回血）
    //   - 周期性在随机位置召唤骷髅小兵
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class NecromancerAI : MonoBehaviour
    {
        public Transform target;

        [Header("Movement")]
        public float preferredMinDist = 5f;
        public float preferredMaxDist = 8f;

        [Header("Soul Drain")]
        public float drainCooldown  = 3f;
        public float drainRange     = 7f;
        public float drainDamage    = 18f;
        public float drainHealRatio = 0.6f;   // 吸取伤害量的60%回血

        [Header("Raise Skeleton")]
        public float summonCooldown  = 10f;
        public int   summonCount_P1  = 1;
        public int   summonCount_P2  = 2;

        // GameBootstrap注入：在指定位置生成并注册一只骷髅
        public System.Func<Vector3, GameObject> SpawnSkeletonCallback;

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private Health         _health;

        private float _lastDrainTime  = -100f;
        private float _lastSummonTime = -100f;
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
            if (Time.time >= _lastDrainTime  + drainCooldown) CastSoulDrain();
            float summonCd = _isPhase2 ? summonCooldown * 0.6f : summonCooldown;
            if (Time.time >= _lastSummonTime + summonCd) RaiseSkeleton();
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

        // HP≤50% 进入Phase2：召唤速度提升，一次多召唤1只
        private void CheckPhase2()
        {
            if (_phase2Triggered || _health == null) return;
            if (_health.Current <= _health.Max * 0.5f)
            {
                _phase2Triggered = true;
                _isPhase2        = true;
            }
        }

        // 灵魂汲取：远程魔法攻击 + 按比例回血
        private void CastSoulDrain()
        {
            _lastDrainTime = Time.time;
            if (target == null) return;

            Vector2 dir  = ((Vector2)target.position - (Vector2)transform.position).normalized;
            var     hits = Physics2D.RaycastAll(transform.position, dir, drainRange);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;
                var d = hit.collider.GetComponent<IDamageable>();
                if (d == null) continue;
                d.TakeDamage(new DamageInfo { Amount = drainDamage, Type = DamageType.Magical, Source = gameObject });
                _health?.Heal(drainDamage * drainHealRatio);
                break;
            }
        }

        // 召唤骷髅：在随机位置生成（委托 GameBootstrap 执行）
        private void RaiseSkeleton()
        {
            _lastSummonTime = Time.time;
            if (SpawnSkeletonCallback == null) return;

            int count = _isPhase2 ? summonCount_P2 : summonCount_P1;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
                SpawnSkeletonCallback(transform.position + (Vector3)offset);
            }
        }
    }
}
