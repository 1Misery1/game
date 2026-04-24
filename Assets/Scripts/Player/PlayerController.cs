using Game.Combat;
using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CharacterStats))]
    public class PlayerController : MonoBehaviour
    {
        private Rigidbody2D _rb;
        private CharacterStats _stats;
        private Vector2 _moveInput;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _stats = GetComponent<CharacterStats>();
        }

        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        private void FixedUpdate()
        {
            float speed = _stats.Get(StatType.MoveSpeed);
            _rb.velocity = _moveInput * speed;
        }
    }
}
