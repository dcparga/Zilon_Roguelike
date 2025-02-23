﻿using Assets.Zilon.Scripts.Services;
using UnityEngine;
using UnityEngine.EventSystems;

using Zenject;

using Zilon.Core.Client;
using Zilon.Core.Commands;
using Zilon.Core.Props;

public class InventorySlotDropHandler : UIBehaviour, IDropHandler
{
    [Inject] private readonly SpecialCommandManager _specialCommandManager;
    [Inject] private readonly ICommandManager _commandManager;
    [Inject] private readonly IInventoryState _inventoryState;

    public InventorySlotVm InventorySlotViewModel;

    public void OnDrop(PointerEventData eventData)
    {
        var slotIndex = InventorySlotViewModel.SlotIndex;

        var equipCommand = _specialCommandManager.GetEquipCommand(slotIndex);

        if (equipCommand.CanExecute())
        {
            var droppedPropItem = eventData.pointerDrag?.GetComponent<PropItemVm>();
            if (droppedPropItem != null)
            {
                // Значит экипировка произошла из инвентаря.
                _inventoryState.SelectedProp = droppedPropItem;

                _commandManager.Push(equipCommand);

                return;
            }

            var droppedInventorySlot = eventData.pointerDrag?.GetComponent<InventorySlotVm>();
            if (droppedInventorySlot != null)
            {
                _inventoryState.SelectedProp = droppedInventorySlot;

                _commandManager.Push(equipCommand);

                return;
            }
        }
    }
}
