﻿using System;

using Zilon.Core.Props;
using Zilon.Core.Schemes;
using Zilon.Core.Tactics;
using Zilon.Core.Tactics.Spatial;

namespace Zilon.Core.MapGenerators
{
    public class SectorFactory : ISectorFactory
    {
        private readonly IActorManager _actorManager;
        private readonly IPropContainerManager _propContainerManager;
        private readonly IDropResolver _dropResolver;
        private readonly ISchemeService _schemeService;
        private readonly IEquipmentDurableService _equipmentDurableService;

        public SectorFactory(IActorManager actorManager,
            IPropContainerManager propContainerManager,
            IDropResolver dropResolver,
            ISchemeService schemeService,
            IEquipmentDurableService equipmentDurableService)
        {
            _actorManager = actorManager ?? throw new ArgumentNullException(nameof(actorManager));
            _propContainerManager = propContainerManager ?? throw new ArgumentNullException(nameof(propContainerManager));
            _dropResolver = dropResolver ?? throw new ArgumentNullException(nameof(dropResolver));
            _schemeService = schemeService ?? throw new ArgumentNullException(nameof(schemeService));
            _equipmentDurableService = equipmentDurableService;
        }

        public ISector Create(ISectorMap map)
        {
            var sector = new Sector(map,
                _actorManager,
                _propContainerManager,
                _dropResolver,
                _schemeService,
                _equipmentDurableService);
            return sector;
        }
    }
}
