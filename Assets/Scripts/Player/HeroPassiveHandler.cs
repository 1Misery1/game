using Game.Combat;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Player
{
    public class HeroPassiveHandler : MonoBehaviour
    {
        public HeroPassiveType PassiveType = HeroPassiveType.None;

        private CharacterStats      _stats;
        private Health              _health;
        private PlayerWeaponHandler _weapons;

        // BattlefieldWill
        private float _battlefieldWillCooldown;

        // ComboStrike
        private int    _comboStacks;
        private float  _comboTimer;
        private object _comboSource;
        private const float ComboWindow    = 2f;
        private const int   MaxComboStacks = 5;

        private void Start()
        {
            _stats   = GetComponent<CharacterStats>();
            _health  = GetComponent<Health>();
            _weapons = GetComponent<PlayerWeaponHandler>();
            InitPassive();
        }

        private void InitPassive()
        {
            switch (PassiveType)
            {
                case HeroPassiveType.EagleEye:
                    _stats.AddModifier(new StatModifier(StatType.CritRate,   ModifierOp.Flat, 0.20f, this));
                    _stats.AddModifier(new StatModifier(StatType.CritDamage, ModifierOp.Flat, 0.30f, this));
                    break;

                case HeroPassiveType.BattlefieldWill:
                    if (_health != null) _health.OnDamaged += OnWarriorDamaged;
                    break;

                case HeroPassiveType.ComboStrike:
                    _comboSource = new object();
                    if (_weapons != null) _weapons.OnNormalAttackFired += OnRangerAttack;
                    break;

                case HeroPassiveType.ManaAmplification:
                    if (_weapons != null) _weapons.OnSkillFired += OnMageSkillFired;
                    break;

                case HeroPassiveType.SacredOath:
                    PlayerPassiveEvents.OnPlayerKilledEnemy += OnPaladinKill;
                    break;
            }
        }

        private void Update()
        {
            switch (PassiveType)
            {
                case HeroPassiveType.BattlefieldWill:
                    if (_battlefieldWillCooldown > 0f) _battlefieldWillCooldown -= Time.deltaTime;
                    break;

                case HeroPassiveType.ComboStrike:
                    if (_comboStacks > 0)
                    {
                        _comboTimer -= Time.deltaTime;
                        if (_comboTimer <= 0f) ResetCombo();
                    }
                    break;
            }
        }

        private void OnDestroy()
        {
            if (_health  != null) _health.OnDamaged              -= OnWarriorDamaged;
            if (_weapons != null)
            {
                _weapons.OnNormalAttackFired -= OnRangerAttack;
                _weapons.OnSkillFired        -= OnMageSkillFired;
            }
            PlayerPassiveEvents.OnPlayerKilledEnemy -= OnPaladinKill;
        }

        // HP ≤30% triggers a 30% max HP burst heal (10s cooldown)
        private void OnWarriorDamaged(DamageInfo _)
        {
            if (_health == null || _battlefieldWillCooldown > 0f) return;
            if (_health.Ratio <= 0.30f)
            {
                _health.Heal(_health.Max * 0.30f);
                _battlefieldWillCooldown = 10f;
            }
        }

        // Each attack within 2s adds a stack (+5% ATK per stack, max 5)
        private void OnRangerAttack()
        {
            _comboTimer = ComboWindow;
            int next = Mathf.Min(_comboStacks + 1, MaxComboStacks);
            if (next == _comboStacks) return;
            _comboStacks = next;
            _stats?.RemoveModifiersFrom(_comboSource);
            _stats?.AddModifier(new StatModifier(StatType.Attack, ModifierOp.PercentAdd, _comboStacks * 0.05f, _comboSource));
        }

        private void ResetCombo()
        {
            _comboStacks = 0;
            _stats?.RemoveModifiersFrom(_comboSource);
        }

        // Next normal attack after using a weapon skill deals double damage
        private void OnMageSkillFired()
        {
            if (_weapons != null) _weapons.BonusDamageMultiplier = 2f;
        }

        // Kill an enemy → restore 5 HP
        private void OnPaladinKill()
        {
            _health?.Heal(5f);
        }
    }
}
