using Game.Data;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Dungeon
{
    [RequireComponent(typeof(Collider2D))]
    public class ShopPedestal : MonoBehaviour
    {
        public TalentData talent;
        public int price = 20;
        public System.Func<int> GetCoins;
        public System.Action<int> SpendCoins;
        public System.Action<TalentData> OnPurchased;

        private bool _inRange;
        private bool _purchased;
        private SpriteRenderer _sr;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null) _inRange = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null) _inRange = false;
        }

        private void Update()
        {
            if (_purchased || !_inRange) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.eKey.wasPressedThisFrame) TryBuy();
        }

        private void TryBuy()
        {
            int coins = GetCoins != null ? GetCoins() : 0;
            if (coins < price)
            {
                StartCoroutine(FlashRed());
                return;
            }
            SpendCoins?.Invoke(price);
            OnPurchased?.Invoke(talent);
            _purchased = true;
            Destroy(gameObject);
        }

        private System.Collections.IEnumerator FlashRed()
        {
            if (_sr == null) yield break;
            var original = _sr.color;
            _sr.color = new Color(1f, 0.2f, 0.2f);
            yield return new WaitForSeconds(0.15f);
            if (_sr != null) _sr.color = original;
        }

        private void OnGUI()
        {
            if (_purchased || talent == null) return;
            if (Camera.main == null) return;

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.9f);
            if (screen.z < 0) return;
            float x = screen.x - 90f;
            float y = Screen.height - screen.y - 20f;

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };
            GUI.Label(new Rect(x, y, 180, 20), $"{talent.talentName}  [{price}c]", title);

            if (_inRange)
            {
                var hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(x, y + 20f, 180, 20), "[E] to buy", hint);
            }
        }
    }
}
