﻿using System;
using System.Collections.Generic;
using System.Linq;

using Assets.Zilon.Scripts;

using JetBrains.Annotations;

using UnityEngine;
using UnityEngine.UI;

using Zenject;

using Zilon.Core.Client;
using Zilon.Core.Commands;
using Zilon.Core.Props;

public class InventoryModalBody : MonoBehaviour, IModalWindowHandler
{
    private readonly List<PropItemVm> _propViewModels;

    public Transform InventoryItemsParent;
    public PropItemVm PropItemPrefab;
    public Transform EquipmentSlotsParent;
    public InventorySlotVm EquipmentSlotPrefab;
    public PropInfoPopup PropInfoPopup;
    public GameObject UseButton;
    public Text DetailText;

    [NotNull] [Inject] private DiContainer _diContainer;
    [NotNull] [Inject] private IPlayerState _playerState;
    [NotNull] [Inject] private IInventoryState _inventoryState;
    [NotNull] [Inject] private ICommandManager _commandManager;
    [NotNull] [Inject(Id = "use-self-command")] private readonly ICommand _useSelfCommand;

    public event EventHandler Closed;

    public InventoryModalBody()
    {
        _propViewModels = new List<PropItemVm>();
    }

    public string Caption => "Inventory";

    public void Start()
    {
        CreateSlots();
    }

    private void CreateSlots()
    {
        var actorViewModel = _playerState.ActiveActor;
        var slots = actorViewModel.Actor.Person.EquipmentCarrier.Slots;

        for (var i = 0; i < slots.Length; i++)
        {
            var slotObject = _diContainer.InstantiatePrefab(EquipmentSlotPrefab, EquipmentSlotsParent);
            var slotViewModel = slotObject.GetComponent<InventorySlotVm>();
            slotViewModel.SlotIndex = i;
            slotViewModel.SlotTypes = slots[i].Types;
            slotViewModel.Click += SlotOnClick;
        }
    }

    public void Init()
    {
        // изначально скрываем кнопку использования
        UseButton.SetActive(false);

        var actor = _playerState.ActiveActor.Actor;
        var inventory = actor.Person.Inventory;
        UpdatePropsInner(InventoryItemsParent, inventory.CalcActualItems());

        inventory.Added += Inventory_Added;
        inventory.Removed += Inventory_Removed;
        inventory.Changed += Inventory_Changed;
    }

    public void ApplyChanges()
    {
        var actor = _playerState.ActiveActor.Actor;
        var inventory = actor.Person.Inventory;
        inventory.Added -= Inventory_Added;
        inventory.Removed -= Inventory_Removed;
        inventory.Changed -= Inventory_Changed;

        _inventoryState.SelectedProp = null;
        _propViewModels.Clear();
    }

    public void CancelChanges()
    {
        _inventoryState.SelectedProp = null;
        _propViewModels.Clear();

        throw new NotImplementedException();
    }

    private void SlotOnClick(object sender, EventArgs e)
    {
        var slotVm = sender as InventorySlotVm;
        if (slotVm == null)
        {
            throw new NotSupportedException();
        }

        slotVm.ApplyEquipment();
    }

    private void Inventory_Removed(object sender, PropStoreEventArgs e)
    {
        foreach (var removedProp in e.Props)
        {
            var propViewModel = _propViewModels.Single(x => x.Prop == removedProp);
            _propViewModels.Remove(propViewModel);
            Destroy(propViewModel.gameObject);

            var isRemovedPropWasSelected = ReferenceEquals(propViewModel, _inventoryState.SelectedProp);
            if (isRemovedPropWasSelected)
            {
                _inventoryState.SelectedProp = null;
                UseButton.SetActive(false);
            }
        }
    }

    private void Inventory_Changed(object sender, PropStoreEventArgs e)
    {
        foreach (var changedProp in e.Props)
        {
            var propViewModel = _propViewModels.Single(x => x.Prop == changedProp);
            propViewModel.UpdateProp();
        }
    }

    private void Inventory_Added(object sender, PropStoreEventArgs e)
    {
        foreach (var newProp in e.Props)
        {
            CreatePropObject(InventoryItemsParent, newProp);
        }
    }

    private void InventoryOnContentChanged(object sender, PropStoreEventArgs e)
    {
        var actor = _playerState.ActiveActor.Actor;
        var inventory = actor.Person.Inventory;
        UpdatePropsInner(InventoryItemsParent, inventory.CalcActualItems());
    }

    private void UpdatePropsInner(Transform itemsParent, IEnumerable<IProp> props)
    {
        foreach (Transform itemTranform in itemsParent)
        {
            Destroy(itemTranform.gameObject);
        }

        foreach (var prop in props)
        {
            CreatePropObject(itemsParent, prop);
        }

        var parentRect = itemsParent.GetComponent<RectTransform>();
        var rowCount = (int)Math.Ceiling(props.Count() / 4f);
        parentRect.sizeDelta = new Vector2(parentRect.sizeDelta.x, (40 + 5) * rowCount);
    }

    private void CreatePropObject(Transform itemsParent, IProp prop)
    {
        var propItemViewModel = Instantiate(PropItemPrefab, itemsParent);
        propItemViewModel.Init(prop);
        propItemViewModel.Click += PropItem_Click;
        propItemViewModel.MouseEnter += PropItemViewModel_MouseEnter;
        propItemViewModel.MouseExit += PropItemViewModel_MouseExit;
        _propViewModels.Add(propItemViewModel);
    }

    private void PropItemViewModel_MouseExit(object sender, EventArgs e)
    {
        //PropInfoPopup.SetPropViewModel(null);
    }

    private void PropItemViewModel_MouseEnter(object sender, EventArgs e)
    {
        var currentItemVm = (PropItemVm)sender;
        PropInfoPopup.SetPropViewModel(currentItemVm);
    }

    //TODO Дубликат с ContainerModalBody.PropItemOnClick
    private void PropItem_Click(object sender, EventArgs e)
    {
        var currentItemVm = (PropItemVm)sender;
        var parentTransform = currentItemVm.transform.parent;
        foreach (var propViewModel in _propViewModels)
        {
            var isSelected = propViewModel == currentItemVm;
            propViewModel.SetSelectedState(isSelected);
        }

        // этот фрагмент - не дубликат
        var canUseProp = currentItemVm.Prop.Scheme.Use != null;
        UseButton.SetActive(canUseProp);

        var propTitle = currentItemVm.Prop.Scheme.Name.En ?? currentItemVm.Prop.Scheme.Name.Ru;
        DetailText.text = propTitle;
        // --- этот фрагмент - не дубликат

        _inventoryState.SelectedProp = currentItemVm;

        if (currentItemVm.Prop == currentItemVm)
        {
            PropInfoPopup.SetPropViewModel(null);
        }
        else
        {
            PropInfoPopup.SetPropViewModel(currentItemVm);
        }
    }

    public void UseButton_Handler()
    {
        _commandManager.Push(_useSelfCommand);
    }
}
