using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Warlord.Domain.Bots;
using Warlord.Gameplay.Bots;
using Warlord.Gameplay.Match;
using Warlord.Gameplay.Players;

namespace Warlord.EditorTools
{
    /// <summary>
    /// Заглянуть ботам в голову прямо во время матча. Без этого настройка характеров идёт
    /// вслепую: со стороны «стоит на месте» и «идёт к цели, но упёрся» выглядят одинаково,
    /// а причины у них разные.
    ///
    /// Читает и ничего не меняет, поэтому безопасно нажимать в любой момент.
    /// </summary>
    public static class WarlordBotDebug
    {
        [MenuItem("Warlord/Боты/Показать решения ботов", priority = 100)]
        public static void DumpBots()
        {
            MatchManager manager = MatchManager.Instance;

            if (manager == null || manager.ServerContext == null)
            {
                Debug.LogWarning("Warlord: матч не идёт — смотреть нечего. Зайдите в play mode и начните бой.");
                return;
            }

            BotSystem system = manager.Bots;

            if (system == null || !system.HasBots)
            {
                Debug.Log("Warlord: в этом матче ботов нет.");
                return;
            }

            IReadOnlyList<BotPlayer> bots = system.Bots;
            StringBuilder report = new();

            report.Append("Warlord: боты в матче — ").Append(bots.Count).AppendLine();

            for (int i = 0; i < bots.Count; i++)
            {
                BotPlayer bot = bots[i];
                PlayerState player = bot.Player;

                if (player == null)
                    continue;

                BotObjective goal = bot.Goal;

                report
                    .Append("  [").Append(bot.Slot).Append("] ")
                    .Append(bot.Profile.Name)
                    .Append(" · ").Append(bot.Profile.Personality != null ? bot.Profile.Personality.displayName : "?")
                    .Append(" · ").Append(bot.Profile.Level)
                    .AppendLine();

                report
                    .Append("        цель: ").Append(goal.Kind)
                    .Append(goal.PointIndex >= 0 ? $" (точка {goal.PointIndex})" : string.Empty)
                    .Append(goal.TargetSlot >= 0 ? $" (против {goal.TargetSlot})" : string.Empty)
                    .Append($", оценка {goal.Score:0.00}")
                    .AppendLine();

                report
                    .Append("        золото ").Append(player.Gold)
                    .Append(", опыт ").Append(player.Xp)
                    .Append(", армия ").Append(player.ArmyCount)
                    .Append(" + гарнизон ").Append(player.GarrisonCount)
                    .Append(" + очередь ").Append(player.BuildQueue != null ? player.BuildQueue.PendingCount : 0)
                    .Append(" из ").Append(player.UnitCap)
                    .AppendLine();

                report
                    .Append("        на базе: ").Append(player.HeroInBuyZone ? "да" : "нет")
                    .Append(", полководец: ")
                    .Append(bot.Hero != null && bot.Hero.IsAlive ? $"жив {bot.Hero.Health}/{bot.Hero.MaxHealth}" : "мёртв")
                    .AppendLine();
            }

            Debug.Log(report.ToString());
        }
    }
}
