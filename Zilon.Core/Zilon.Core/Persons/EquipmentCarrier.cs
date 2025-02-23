﻿using System;
using System.Collections.Generic;

using JetBrains.Annotations;

using Zilon.Core.Props;
using Zilon.Core.Schemes;

namespace Zilon.Core.Persons
{
    public class EquipmentCarrier : EquipmentCarrierBase
    {
        public override PersonSlotSubScheme[] Slots { get; protected set; }

        public EquipmentCarrier([NotNull] [ItemNotNull] IEnumerable<PersonSlotSubScheme> slots): base(slots)
        {
            
        }

        protected override void ValidateSetEquipment(Equipment equipment, int slotIndex)
        {
            var slot = Slots[slotIndex];

            if (!EquipmentCarrierHelper.CheckSlotCompability(equipment, slot))
            {
                throw new ArgumentException($"Для экипировки указан слот {slot}, не подходящий для данного типа предмета {equipment}.");
            }

            if (!EquipmentCarrierHelper.CheckDualCompability(this, equipment, slot, slotIndex))
            {
                throw new InvalidOperationException($"Попытка экипировать предмет {equipment}, несовместимый с текущий экипировкой.");
            }

            if (!EquipmentCarrierHelper.CheckShieldCompability(this, equipment, slot, slotIndex))
            {
                throw new InvalidOperationException("Попытка экипировать два щита.");
            }
        }
    }
}
