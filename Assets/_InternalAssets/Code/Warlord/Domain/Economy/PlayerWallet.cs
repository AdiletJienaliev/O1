using UnityEngine;

namespace Warlord.Domain.Economy
{
    /// <summary>
    /// Кошелёк игрока. Внутри копит дробные значения, наружу отдаёт целые:
    /// иначе доход 8/сек при такте 20 Гц терял бы копейки на каждом тике.
    /// Живёт только на сервере, клиент видит округлённые SyncVar.
    /// </summary>
    public sealed class PlayerWallet
    {
        private double _gold;
        private double _xpEarned;
        private int _xpSpent;

        public PlayerWallet(int startingGold) => _gold = Mathf.Max(0, startingGold);

        public int Gold => (int)_gold;

        /// <summary>Весь заработанный XP — используется в статистике матча.</summary>
        public int XpEarned => (int)_xpEarned;

        /// <summary>Доступный для покупки перков XP.</summary>
        public int XpAvailable => Mathf.Max(0, (int)_xpEarned - _xpSpent);

        public void Accrue(in IncomeProfile income, float deltaTime)
        {
            _gold += income.GoldPerSecond * deltaTime;
            _xpEarned += income.XpPerSecond * deltaTime;
        }

        public void AddGold(int amount)
        {
            if (amount > 0)
                _gold += amount;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || _gold < amount)
                return false;

            _gold -= amount;
            return true;
        }

        public bool TrySpendXp(int amount)
        {
            if (amount < 0 || XpAvailable < amount)
                return false;

            _xpSpent += amount;
            return true;
        }

        /// <summary>Захват базы забирает всё золото жертвы (ГДД §10). Возвращает переданную сумму.</summary>
        public int DrainGold(float fraction)
        {
            int taken = Mathf.RoundToInt((float)(_gold * Mathf.Clamp01(fraction)));
            _gold -= taken;
            if (_gold < 0d)
                _gold = 0d;

            return taken;
        }
    }
}
