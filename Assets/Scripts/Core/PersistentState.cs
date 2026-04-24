using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public class PersistentState
    {
        public List<string> UnlockedHeroIds = new List<string>();
        public int UnlockCurrency = 0;

        private const string FileName = "save.json";
        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public bool IsHeroUnlocked(string heroId) => UnlockedHeroIds.Contains(heroId);

        public bool TryUnlockHero(string heroId, int cost)
        {
            if (UnlockedHeroIds.Contains(heroId)) return false;
            if (UnlockCurrency < cost) return false;
            UnlockCurrency -= cost;
            UnlockedHeroIds.Add(heroId);
            Save();
            return true;
        }

        public void AddCurrency(int amount)
        {
            UnlockCurrency += amount;
            Save();
        }

        public void Save()
        {
            var json = JsonUtility.ToJson(this, true);
            File.WriteAllText(SavePath, json);
        }

        public static PersistentState Load()
        {
            if (!File.Exists(SavePath)) return new PersistentState();
            try
            {
                var json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<PersistentState>(json) ?? new PersistentState();
            }
            catch
            {
                return new PersistentState();
            }
        }
    }
}
