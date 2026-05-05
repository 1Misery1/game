using Game.Data;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Dungeon
{
    [RequireComponent(typeof(Collider2D))]
    public class WeaponPedestal : MonoBehaviour
    {
        public WeaponInstance          Weapon;
        public System.Action<WeaponInstance> OnEquipped;

        private bool _inRange;
        private bool _taken;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
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
            if (_taken || !_inRange) return;
            if (Keyboard.current?.eKey.wasPressedThisFrame == true)
            {
                _taken = true;
                OnEquipped?.Invoke(Weapon);
                Destroy(gameObject);
            }
        }

        private void OnGUI()
        {
            if (_taken || Weapon?.Data == null || Camera.main == null) return;
            var data = Weapon.Data;

            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.95f);
            if (screen.z < 0) return;

            float x = screen.x - 100f;
            float y = Screen.height - screen.y - 66f;

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal    = { textColor = WeaponData.GetRarityColor(data.rarity) }
            };
            GUI.Label(new Rect(x, y, 200, 20), $"{data.weaponName}  [{Weapon.CategoryLabel}]", nameStyle);

            var statStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = new Color(0.85f, 0.9f, 1f) }
            };
            GUI.Label(new Rect(x, y + 19f, 200, 18),
                $"{Weapon.EffectiveDamage:0}dmg  {data.attackSpeed:0.0}/s", statStyle);

            if (data.HasSkill)
            {
                var skStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11, alignment = TextAnchor.MiddleCenter,
                    normal   = { textColor = new Color(0.65f, 0.85f, 1f) }
                };
                GUI.Label(new Rect(x, y + 37f, 200, 16), $"技能: {data.skill.skillName}", skStyle);
            }

            if (_inRange)
            {
                float hintY = data.HasSkill ? y + 53f : y + 37f;
                var hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, alignment = TextAnchor.MiddleCenter,
                    normal   = { textColor = Color.white }
                };
                GUI.Label(new Rect(x, hintY, 200, 18), "[E] 装备到当前槽", hint);
            }
        }
    }
}
