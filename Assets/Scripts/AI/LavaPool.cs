using System.Collections.Generic;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.AI
{
    // 地狱巨人·岩浆池：踩入持续扣血，到达生命周期后消失
    public class LavaPool : MonoBehaviour
    {
        public float damagePerSecond = 8f;
        public float lifetime = 5f;
        public GameObject owner;

        private float _elapsed;
        private float _tickTimer;
        private const float TickInterval = 0.4f;

        private readonly HashSet<IDamageable> _inside = new HashSet<IDamageable>();

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            _tickTimer += Time.deltaTime;
            if (_tickTimer >= TickInterval)
            {
                _tickTimer -= TickInterval;
                float tickDmg = damagePerSecond * TickInterval;
                foreach (var d in _inside)
                {
                    if (d == null) continue;
                    d.TakeDamage(new DamageInfo
                    {
                        Amount = tickDmg,
                        Type = DamageType.True,
                        IsCrit = false,
                        Source = owner
                    });
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner != null && other.gameObject == owner) return;
            var d = other.GetComponent<IDamageable>();
            if (d != null) _inside.Add(d);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var d = other.GetComponent<IDamageable>();
            if (d != null) _inside.Remove(d);
        }
    }
}
