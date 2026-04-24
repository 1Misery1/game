using System.Collections.Generic;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.Dungeon
{
    public class RoomController : MonoBehaviour
    {
        [SerializeField] private RoomType roomType = RoomType.Monster;
        [SerializeField] private List<Health> enemies = new List<Health>();

        public RoomType Type => roomType;
        public bool IsCleared { get; private set; }

        public System.Action<RoomController> OnCleared;

        private int _aliveCount;

        private void Start()
        {
            _aliveCount = 0;
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                _aliveCount++;
                enemy.OnDied += HandleEnemyDied;
            }

            if (_aliveCount == 0 && roomType != RoomType.Boss)
            {
                MarkCleared();
            }
        }

        private void HandleEnemyDied()
        {
            _aliveCount--;
            if (_aliveCount <= 0) MarkCleared();
        }

        private void MarkCleared()
        {
            if (IsCleared) return;
            IsCleared = true;
            OnCleared?.Invoke(this);
        }
    }
}
