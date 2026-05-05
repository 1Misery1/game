using Game.Combat;
using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats), typeof(PlayerWeaponHandler))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Color attackFlashColor  = Color.yellow;
        [SerializeField] private Color skillFlashColor   = new Color(0.5f, 0.5f, 1f);
        [SerializeField] private Color switchFlashColor  = new Color(0.4f, 1f, 0.8f);

        private Rigidbody2D        _rb;
        private CharacterStats     _stats;
        private PlayerWeaponHandler _weapons;
        private SpriteRenderer     _sr;
        private Color _baseColor;
        private Color _flashColor;
        private float _flashUntil;
        private float _stunUntil;

        public void ApplyStun(float duration)
        {
            _stunUntil = Mathf.Max(_stunUntil, Time.time + duration);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _stats   = GetComponent<CharacterStats>();
            _weapons = GetComponent<PlayerWeaponHandler>();
            _sr      = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        private void Update()
        {
            var kb    = Keyboard.current;
            var mouse = Mouse.current;

            bool stunned = Time.time < _stunUntil;

            bool attackPressed = !stunned &&
                ((kb    != null && kb.spaceKey.wasPressedThisFrame) ||
                 (mouse != null && mouse.leftButton.wasPressedThisFrame));

            bool skillPressed = !stunned &&
                ((kb    != null && kb.rKey.wasPressedThisFrame) ||
                 (mouse != null && mouse.rightButton.wasPressedThisFrame));

            Vector2 aimDir = GetAimDirection();

            if (attackPressed && _weapons.TryAttack(aimDir))
                Flash(attackFlashColor);

            if (skillPressed && _weapons.TryUseSkill(aimDir))
                Flash(skillFlashColor);

            if (_sr != null)
                _sr.color = Time.time < _flashUntil ? _flashColor : _baseColor;
        }

        private void FixedUpdate()
        {
            if (Time.time < _stunUntil)
            {
                _rb.velocity = Vector2.zero;
                return;
            }
            var kb   = Keyboard.current;
            var move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  move.x -= 1f;
                if (move.sqrMagnitude > 1f) move.Normalize();
            }
            _rb.velocity = move * _stats.Get(StatType.MoveSpeed);
        }

        private Vector2 GetAimDirection()
        {
            if (Camera.main == null) return Vector2.right;
            var mouse = Mouse.current;
            if (mouse == null) return Vector2.right;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(
                new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, 0f));
            Vector2 dir = (Vector2)(worldPos - transform.position);
            return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
        }

        private void Flash(Color color)
        {
            _flashColor = color;
            _flashUntil = Time.time + 0.12f;
        }
    }
}
