using System;
using Warlord.Core;

namespace Warlord.Domain.Upgrades
{
    /// <summary>
    /// Уровни всех веток древа одного игрока. Упакованы в шесть байт — это же значение
    /// уходит по сети как единый SyncVar и служит ключом кэша статов.
    /// </summary>
    [Serializable]
    public struct UpgradeLevels : IEquatable<UpgradeLevels>
    {
        public byte Damage;
        public byte Health;
        public byte AttackRange;
        public byte UnitCap;
        public byte MoveSpeed;
        public byte Hero;

        public int Get(UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Damage: return Damage;
                case UpgradeBranch.Health: return Health;
                case UpgradeBranch.AttackRange: return AttackRange;
                case UpgradeBranch.UnitCap: return UnitCap;
                case UpgradeBranch.MoveSpeed: return MoveSpeed;
                case UpgradeBranch.Hero: return Hero;
                default: return 0;
            }
        }

        /// <summary>Возвращает копию с изменённым уровнем ветки. Структура иммутабельна по смыслу.</summary>
        public UpgradeLevels With(UpgradeBranch branch, int level)
        {
            UpgradeLevels copy = this;
            byte value = (byte)(level < 0 ? 0 : level > byte.MaxValue ? byte.MaxValue : level);

            switch (branch)
            {
                case UpgradeBranch.Damage: copy.Damage = value; break;
                case UpgradeBranch.Health: copy.Health = value; break;
                case UpgradeBranch.AttackRange: copy.AttackRange = value; break;
                case UpgradeBranch.UnitCap: copy.UnitCap = value; break;
                case UpgradeBranch.MoveSpeed: copy.MoveSpeed = value; break;
                case UpgradeBranch.Hero: copy.Hero = value; break;
            }

            return copy;
        }

        public int TotalLevels => Damage + Health + AttackRange + UnitCap + MoveSpeed + Hero;

        public bool Equals(UpgradeLevels other)
        {
            return Damage == other.Damage
                && Health == other.Health
                && AttackRange == other.AttackRange
                && UnitCap == other.UnitCap
                && MoveSpeed == other.MoveSpeed
                && Hero == other.Hero;
        }

        public override bool Equals(object obj) => obj is UpgradeLevels other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Damage;
            hash = hash * 31 + Health;
            hash = hash * 31 + AttackRange;
            hash = hash * 31 + UnitCap;
            hash = hash * 31 + MoveSpeed;
            hash = hash * 31 + Hero;
            return hash;
        }
    }
}
