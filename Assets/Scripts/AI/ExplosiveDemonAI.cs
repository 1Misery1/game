using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 爆炎恶魔：追击玩家，进入引爆范围后开始倒计时，倒计时结束或被击杀时爆炸
    // 死亡爆炸由 GameBootstrap.AttachSpecialDeathEffect 处理（HasExploded=true 时跳过）
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class ExplosiveDemonAI : MonoBehaviour
    {
        public Transform target;

        [Header("Chase")]
        public float stoppingDistance = 0.8f;

        [Header("Explosion")]
        public float fuseRange       = 1.5f;
        public float fuseDuration    = 1.5f;
        public float explosionRadius = 3f;
        public float explosionDamage = 40f;

        public bool HasExploded { get; private set; }

        private Rigidbody2D    _rb;
        private CharacterStats _stats;
        private SpriteRenderer _sr;
        private Color          _baseColor;
        private float          _fuseTimer;
        private bool           _fuseActive;

        private void Awake()
        {
            _rb    = GetComponent<Rigidbody2D>();
            _stats = GetComponent<CharacterStats>();
            _sr    = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        private void Update()
        {
            if (target == null || HasExploded) return;

            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= fuseRange)
            {
                _fuseActive  = true;
                _fuseTimer  += Time.deltaTime;
                // 颜色随引信进度渐变为亮红
                if (_sr != null)
                    _sr.color = Color.Lerp(_baseColor, new Color(1f, 0.1f, 0.1f), _fuseTimer / fuseDuration);

                if (_fuseTimer >= fuseDuration)
                    Explode();
            }
            else
            {
                _fuseActive = false;
                _fuseTimer  = 0f;
                if (_sr != null) _sr.color = _baseColor;
            }
        }

        private void FixedUpdate()
        {
            if (target == null || HasExploded) return;
            Vector2 delta = (Vector2)target.position - _rb.position;
            float   dist  = delta.magnitude;
            if (dist < 0.001f || dist <= stoppingDistance) return;
            _rb.MovePosition(_rb.position + delta / dist * _stats.Get(StatType.MoveSpeed) * Time.fixedDeltaTime);
        }

        private void Explode()
        {
            HasExploded = true;
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, explosionRadius))
            {
                if (col.GetComponent<EnemyTag>() != null) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = explosionDamage, Type = DamageType.True, Source = gameObject
                });
            }
            // 通过自身受伤触发正规死亡流程（OnDied链）
            GetComponent<Health>()?.TakeDamage(new DamageInfo
            {
                Amount = 99999f, Type = DamageType.True, Source = gameObject
            });
        }
    }
}
