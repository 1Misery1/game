using System.Collections.Generic;
using Game.Data;

namespace Game.Core
{
    public class RunState
    {
        public HeroData Hero { get; private set; }
        public readonly List<WeaponData> Weapons = new List<WeaponData>();
        public readonly List<TalentData> Talents = new List<TalentData>();
        public readonly List<BuffData> Buffs = new List<BuffData>();

        public int CurrentFloor = 1;
        public int Coins = 0;
        public int UnlockCurrencyEarned = 0;
        public bool IsActive { get; private set; }

        public void Begin(HeroData hero)
        {
            Hero = hero;
            Weapons.Clear();
            Talents.Clear();
            Buffs.Clear();
            CurrentFloor = 1;
            Coins = 0;
            UnlockCurrencyEarned = 0;
            IsActive = true;
        }

        public void End()
        {
            IsActive = false;
            Weapons.Clear();
            Talents.Clear();
            Buffs.Clear();
        }

        public void AddWeapon(WeaponData w) => Weapons.Add(w);
        public void AddTalent(TalentData t) => Talents.Add(t);
        public void AddBuff(BuffData b) => Buffs.Add(b);
    }
}
