namespace Game.Core
{
    public static class PlayerPassiveEvents
    {
        public static System.Action OnPlayerKilledEnemy;

        public static void RaisePlayerKilledEnemy() => OnPlayerKilledEnemy?.Invoke();
    }
}
