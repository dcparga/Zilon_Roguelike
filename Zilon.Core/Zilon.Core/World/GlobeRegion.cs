﻿using System.Collections.Generic;
using System.Linq;

using Zilon.Core.Tactics.Spatial;

namespace Zilon.Core.World
{
    /// <summary>
    /// Граф провинции на глобальной карте.
    /// </summary>
    public class GlobeRegion : HexMap
    {
        /// <summary>
        /// Конструктор графа провинции.
        /// </summary>
        public GlobeRegion(int segmentSize) : base(segmentSize)
        {
        }

        /// <summary>
        /// Вспомогательное свойство региона для того, чтобы каждый раз не приводить узлы к ожидаемому типу.
        /// </summary>
        public IEnumerable<GlobeRegionNode> RegionNodes
        {
            get
            {
                return Nodes.OfType<GlobeRegionNode>();
            }
        }
    }
}
