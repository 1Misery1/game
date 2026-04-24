using Game.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public RunState Run { get; private set; } = new RunState();
        public PersistentState Persistent { get; private set; }

        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private string dungeonSceneName = "Dungeon";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Persistent = PersistentState.Load();
        }

        public void StartRun(HeroData hero)
        {
            Run.Begin(hero);
            SceneManager.LoadScene(dungeonSceneName);
        }

        public void EndRun(bool cleared)
        {
            if (cleared)
            {
                Persistent.AddCurrency(Run.UnlockCurrencyEarned);
            }
            Run.End();
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
