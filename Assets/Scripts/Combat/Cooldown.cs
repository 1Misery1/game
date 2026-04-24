using UnityEngine;

namespace Game.Combat
{
    [System.Serializable]
    public struct Cooldown
    {
        [SerializeField] private float duration;
        private float _readyAt;

        public float Duration => duration;
        public float Remaining => Mathf.Max(0f, _readyAt - Time.time);
        public float Ratio => duration > 0f ? Mathf.Clamp01(Remaining / duration) : 0f;
        public bool IsReady => Time.time >= _readyAt;

        public Cooldown(float duration)
        {
            this.duration = duration;
            _readyAt = 0f;
        }

        public bool TryUse(float cdReduction = 0f)
        {
            if (!IsReady) return false;
            _readyAt = Time.time + duration * Mathf.Max(0.1f, 1f - cdReduction);
            return true;
        }

        public void Reset() => _readyAt = 0f;
        public void SetDuration(float d) => duration = d;
    }
}
