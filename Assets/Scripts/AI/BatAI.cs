using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 飞天蝙蝠：绕玩家环绕飞行，冷却结束后俯冲冲刺攻击，命中后后退
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class BatAI : MonoBehaviour
    {
        public Transform target;
        public float orbitRadius   = 3.5f;
        public float orbitSpeed    = 3.5f;  // 环绕角速度 rad/s
        public float dashSpeed     = 16f;
        public float dashDuration  = 0.35f;
        public float dashCooldown  = 3.0f;
        public float dashDamage    = 12f;
        public float retreatSpeed  = 8f;
        public float retreatDuration = 0.8f;

        private Rigidbody2D _rb;
        private float _orbitAngle;
        private float _dashTimer;
        private float _retreatTimer;
        private float _lastDashTime;
        private Vector2 _dashDir;
        private bool _hitThisDash;

        private enum State { Orbit, Dash, Retreat }
        private State _state = State.Orbit;

        private void Awake() => _rb = GetComponent<Rigidbody2D>();

        private void Update()
        {
            if (target == null) return;

            switch (_state)
            {
                case State.Orbit:
                    _orbitAngle += orbitSpeed * Time.deltaTime;
                    if (Time.time >= _lastDashTime + dashCooldown)
                        BeginDash();
                    break;

                case State.Dash:
                    _dashTimer += Time.deltaTime;
                    if (_dashTimer >= dashDuration)
                        BeginRetreat();
                    break;

                case State.Retreat:
                    _retreatTimer += Time.deltaTime;
                    if (_retreatTimer >= retreatDuration)
                        _state = State.Orbit;
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (target == null) return;

            switch (_state)
            {
                case State.Orbit:
                {
                    Vector2 offset  = new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * orbitRadius;
                    Vector2 desired = (Vector2)target.position + offset;
                    _rb.MovePosition(Vector2.MoveTowards(_rb.position, desired, orbitSpeed * Time.fixedDeltaTime));
                    break;
                }
                case State.Dash:
                    _rb.MovePosition(_rb.position + _dashDir * dashSpeed * Time.fixedDeltaTime);
                    break;

                case State.Retreat:
                {
                    Vector2 away = ((Vector2)transform.position - (Vector2)target.position).normalized;
                    _rb.MovePosition(_rb.position + away * retreatSpeed * Time.fixedDeltaTime);
                    break;
                }
            }
        }

        private void BeginDash()
        {
            _state        = State.Dash;
            _dashDir      = ((Vector2)target.position - (Vector2)transform.position).normalized;
            _dashTimer    = 0f;
            _hitThisDash  = false;
            _lastDashTime = Time.time;
        }

        private void BeginRetreat()
        {
            _state        = State.Retreat;
            _retreatTimer = 0f;
        }

        // 冲刺中碰到目标立即造成伤害并开始后退
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_state != State.Dash || _hitThisDash) return;
            if (other.gameObject == gameObject) return;
            var d = other.GetComponent<IDamageable>();
            if (d != null)
            {
                d.TakeDamage(new DamageInfo
                {
                    Amount = dashDamage,
                    Type   = DamageType.Physical,
                    Source = gameObject
                });
                _hitThisDash = true;
                BeginRetreat();
            }
        }
    }
}
