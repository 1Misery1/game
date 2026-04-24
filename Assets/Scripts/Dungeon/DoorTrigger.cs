using Game.Player;
using UnityEngine;

namespace Game.Dungeon
{
    [RequireComponent(typeof(Collider2D))]
    public class DoorTrigger : MonoBehaviour
    {
        public System.Action OnPlayerEntered;
        private bool _used;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_used) return;
            if (other.GetComponent<PlayerController>() == null) return;
            _used = true;
            OnPlayerEntered?.Invoke();
        }
    }
}
