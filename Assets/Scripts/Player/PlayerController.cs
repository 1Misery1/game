using Game.Combat;
using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackBonusDamage = 10f;
        [SerializeField] private float attackInterval = 0.35f;
        [SerializeField] private Color attackFlashColor = Color.yellow;

        private Rigidbody2D _rb;
        private CharacterStats _stats;
        private SpriteRenderer _sr;
        private Color _baseColor;
        private float _lastAttackTime;
        private float _flashUntil;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _stats = GetComponent<CharacterStats>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool attackPressed =
                (kb != null && kb.spaceKey.wasPressedThisFrame) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame);

            if (attackPressed && Time.time >= _lastAttackTime + attackInterval)
            {
                DoAttack();
                _lastAttackTime = Time.time;
            }

            if (_sr != null)
            {
                _sr.color = Time.time < _flashUntil ? attackFlashColor : _baseColor;
            }
        }

        private void FixedUpdate()
        {
            var kb = Keyboard.current;
            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 1f) move.Normalize();
            }
            float speed = _stats.Get(StatType.MoveSpeed);
            _rb.velocity = move * speed;
        }

        private void DoAttack()
        {
            _flashUntil = Time.time + 0.1f;

            float totalDamage = _stats.Get(StatType.Attack) + attackBonusDamage;
            var hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
            foreach (var hit in hits)
            {
                if (hit == null || hit.gameObject == gameObject) continue;
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null) continue;

                damageable.TakeDamage(new DamageInfo
                {
                    Amount = totalDamage,
                    Type = DamageType.Physical,
                    IsCrit = false,
                    Source = gameObject
                });
            }
        }
    }
}
