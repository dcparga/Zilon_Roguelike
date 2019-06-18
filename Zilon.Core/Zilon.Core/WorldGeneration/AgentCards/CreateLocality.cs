﻿using System.Collections.Generic;
using System.Linq;

using Zilon.Core.Common;
using Zilon.Core.CommonServices.Dices;

namespace Zilon.Core.WorldGeneration.AgentCards
{
    /// <summary>
    /// Создание населённого пункта агентом.
    /// </summary>
    /// <remarks>
    /// Населённый пункт будет иметь специальность, близкую к специальности агента.
    /// </remarks>
    public class CreateLocality : IAgentCard
    {
        public int PowerCost { get; }

        public bool CanUse(Agent agent, Globe globe)
        {
            var hasCurrentLocality = globe.LocalitiesCells.TryGetValue(agent.Location, out var currentLocality);
            if (currentLocality != null)
            {
                if (currentLocality.Population >= 2)
                {
                    return true;
                }
            }

            return false;
        }

        public string Use(Agent agent, Globe globe, IDice dice)
        {
            globe.LocalitiesCells.TryGetValue(agent.Location, out var currentLocality);

            var highestBranchs = agent.Skills.OrderBy(x => x.Value)
                                    .Where(x => /*x.Key != BranchType.Politics &&*/ x.Value >= 1);
            if (!highestBranchs.Any())
            {
                return null;
            }

            var firstBranch = highestBranchs.First();

            // Обнаружение свободных узлов для размещения населённого пункта.
            // Свободные узлы ишутся от текущей локации агента.

            TerrainCell freeLocaltion = null;

            var nextCoords = HexHelper.GetOffsetClockwise();
            var agentCubeCoords = HexHelper.ConvertToCube(agent.Location.Coords.X, agent.Location.Coords.Y);
            for (var i = 0; i < nextCoords.Length; i++)
            {
                var scanCubeCoords = agentCubeCoords + nextCoords[i];
                var scanOffsetCoords = HexHelper.ConvertToOffset(scanCubeCoords);

                var freeX = scanOffsetCoords.X;
                var freeY = scanOffsetCoords.Y;

                // Убеждаемся, что проверяемый узел находится в границах мира.
                if (freeX < 0)
                {
                    continue;
                }

                if (freeX >= globe.Terrain.Length)
                {
                    continue;
                }

                if (freeY < 0)
                {
                    continue;
                }

                if (freeY >= globe.Terrain[freeX].Length)
                {
                    continue;
                }

                // Проверка, есть ли в найденной локации населённые пункты.
                var freeLocaltion1 = globe.Terrain[freeX][freeY];

                if (!globe.LocalitiesCells.TryGetValue(freeLocaltion1, out var freeCheckLocality))
                {
                    freeLocaltion = globe.Terrain[freeX][freeY];
                }
            }

            string history;
            if (freeLocaltion != null)
            {
                // Свободный узел был найден.
                // Тогда создаём здесь населённый пункт с доминирующей специаьностью агента.
                // Популяция нового нас.пункта минимальна.
                // Одна единица популяци из текущего нас.пункта снимается.
                // Считается, что часть жителей мигрировали для начала строительства нового нас.пункта.

                var localityName = globe.GetLocalityName(dice);

                var createdLocality = new Locality
                {
                    Name = localityName,
                    Branches = new Dictionary<BranchType, int> { { firstBranch.Key, 1 } },
                    Cell = freeLocaltion,

                    Population = 1,
                    Owner = currentLocality.Owner
                };

                currentLocality.Population--;

                globe.Localities.Add(createdLocality);
                globe.LocalitiesCells[freeLocaltion] = createdLocality;
                globe.ScanResult.Free.Remove(freeLocaltion);

                history = $"{agent} fonded {createdLocality}";
            }
            else
            {
                // Если не удалось найти свободный узел,
                // то агент перемещается в произвольный населённый пункт своего государства.

                var rolledTransportLocality = TransportHelper.RollTargetLocality(globe, dice, agent, currentLocality);

                if (currentLocality != null)
                {
                    Helper.RemoveAgentFromCell(globe.AgentCells, agent.Location, agent);
                }

                agent.Location = rolledTransportLocality.Cell;

                Helper.AddAgentToCell(globe.AgentCells, agent.Location, agent);

                history = $"{agent} change location to {rolledTransportLocality} trying to start new life.";
            }

            return history;
        }
    }
}
