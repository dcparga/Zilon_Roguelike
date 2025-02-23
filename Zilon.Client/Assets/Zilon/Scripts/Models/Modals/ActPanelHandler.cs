﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;
using Zilon.Core.Client;
using Zilon.Core.Persons;

public class ActPanelHandler : MonoBehaviour {

	[Inject] private ISectorUiState _playerState;

	public ActItemVm ActVmPrefab;
	public Transform ActItemParent;

	public void Start()
	{
		var actorVm = _playerState.ActiveActor;
		var actor = actorVm.Actor;
		
		//actor.Person.EquipmentCarrier.EquipmentChanged += EquipmentCarrierOnEquipmentChanged;
		
		var acts = actor.Person.TacticalActCarrier.Acts;
		UpdateActs(acts);
	}

	private void EquipmentCarrierOnEquipmentChanged(object sender, EquipmentChangedEventArgs e)
	{
		var actorVm = _playerState.ActiveActor;
		var actor = actorVm.Actor;
		
		var acts = actor.Person.TacticalActCarrier.Acts;
		UpdateActs(acts);
	}

	private void UpdateActs(IEnumerable<ITacticalAct> acts)
	{
		foreach (Transform item in ActItemParent)
		{
			Destroy(item.gameObject);
		}

		var actArray = acts.ToArray();
		foreach (var act in actArray)
		{
			var actItemVm = Instantiate(ActVmPrefab, ActItemParent);
			actItemVm.Init(act);
			actItemVm.Click += ActClick_Handler;
		}
	}

	private void ActClick_Handler(object sender, EventArgs e)
    {
        var actItemVm = sender as ActItemVm;
        if (actItemVm == null)
        {
            throw new InvalidOperationException("Не указано действие (ViewModel).");
        }

        TacticalAct act = GetAct(actItemVm);
    }

    private static TacticalAct GetAct(ActItemVm actItemVm)
    {
        if (actItemVm.Act is TacticalAct act)
        {
            return act;
        }

        throw new InvalidOperationException("Не указано действие (Domain).");
    }
}
