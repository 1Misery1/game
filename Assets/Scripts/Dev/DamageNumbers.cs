using System.Collections.Generic;
using UnityEngine;

namespace Game.Dev
{
    public class DamageNumbers : MonoBehaviour
    {
        private struct Entry
        {
            public Vector3 worldPos;
            public string  text;
            public Color   color;
            public float   spawnTime;
            public bool    large;
        }

        private const float Lifetime = 1.2f;
        private const float Rise     = 1.8f;

        public static DamageNumbers Instance { get; private set; }
        private readonly List<Entry> _active = new List<Entry>();

        private void Awake()  { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void Show(Vector3 worldPos, float amount, bool crit, bool heal = false)
        {
            Color c = heal ? new Color(0.3f, 1f, 0.45f)
                   : crit ? new Color(1f, 0.95f, 0.1f)
                           : new Color(1f, 0.5f, 0.3f);
            string txt = crit ? $"★{amount:0}!" : $"{amount:0}";
            _active.Add(new Entry
            {
                worldPos  = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), 0.2f, 0f),
                text      = txt,
                color     = c,
                spawnTime = Time.time,
                large     = crit || heal,
            });
        }

        private void OnGUI()
        {
            if (Camera.main == null) return;
            float now = Time.time;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                float age = now - e.spawnTime;
                if (age >= Lifetime) { _active.RemoveAt(i); continue; }
                float t = age / Lifetime;

                var risen  = e.worldPos + Vector3.up * (Rise * age);
                var screen = Camera.main.WorldToScreenPoint(risen);
                if (screen.z < 0) continue;

                Color c = e.color;
                c.a = Mathf.Clamp01(1f - t * 1.15f);

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = e.large ? 20 : 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = c }
                };
                GUI.Label(new Rect(screen.x - 40f, Screen.height - screen.y - 16f, 80f, 32f), e.text, style);
            }
        }
    }
}
