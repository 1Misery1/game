using Game.Data;
using Game.Player;
using UnityEngine;

namespace Game.Dungeon
{
    [RequireComponent(typeof(Collider2D))]
    public class TalentPickup : MonoBehaviour
    {
        public TalentData talent;
        public System.Action<TalentData> OnPicked;
        private bool _picked;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, 120f * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_picked) return;
            if (other.GetComponent<PlayerController>() == null) return;
            _picked = true;
            OnPicked?.Invoke(talent);
            Destroy(gameObject);
        }
    }
}
