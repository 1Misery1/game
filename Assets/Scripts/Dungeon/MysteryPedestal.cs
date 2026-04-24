using Game.Player;
using UnityEngine;

namespace Game.Dungeon
{
    public enum MysteryOutcome
    {
        Lucky,   // +25 coins
        Gift,    // free random talent
        Heal,    // full heal
        Cursed   // -15% Max HP
    }

    [RequireComponent(typeof(Collider2D))]
    public class MysteryPedestal : MonoBehaviour
    {
        public System.Action<MysteryOutcome> OnResolved;
        private bool _resolved;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, 80f * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_resolved) return;
            if (other.GetComponent<PlayerController>() == null) return;
            _resolved = true;
            int count = System.Enum.GetValues(typeof(MysteryOutcome)).Length;
            var outcome = (MysteryOutcome)Random.Range(0, count);
            OnResolved?.Invoke(outcome);
            Destroy(gameObject);
        }
    }
}
