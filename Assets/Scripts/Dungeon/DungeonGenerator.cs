using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Dungeon
{
    public class DungeonGenerator : MonoBehaviour
    {
        [System.Serializable]
        public class RoomWeight
        {
            public RoomType type;
            public GameObject prefab;
            [Range(0f, 10f)] public float weight = 1f;
        }

        [SerializeField] private int roomsPerFloor = 8;
        [SerializeField] private List<RoomWeight> roomPool = new List<RoomWeight>();
        [SerializeField] private GameObject bossRoomPrefab;

        public List<RoomController> Generate(int floor)
        {
            var result = new List<RoomController>();

            for (int i = 0; i < roomsPerFloor - 1; i++)
            {
                var prefab = PickRoomPrefab();
                if (prefab == null) continue;
                var instance = Instantiate(prefab, transform);
                var controller = instance.GetComponent<RoomController>();
                if (controller != null) result.Add(controller);
            }

            if (bossRoomPrefab != null)
            {
                var bossInstance = Instantiate(bossRoomPrefab, transform);
                var bossController = bossInstance.GetComponent<RoomController>();
                if (bossController != null) result.Add(bossController);
            }

            return result;
        }

        private GameObject PickRoomPrefab()
        {
            float total = 0f;
            foreach (var rw in roomPool) total += rw.weight;
            if (total <= 0f) return null;

            float roll = Random.value * total;
            float acc = 0f;
            foreach (var rw in roomPool)
            {
                acc += rw.weight;
                if (roll <= acc) return rw.prefab;
            }
            return roomPool[roomPool.Count - 1].prefab;
        }
    }
}
