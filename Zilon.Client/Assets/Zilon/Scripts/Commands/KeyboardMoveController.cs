﻿using System;
using System.Collections.Generic;
using System.Linq;

using Assets.Zilon.Scripts.Commands;
using Assets.Zilon.Scripts.Services;

using JetBrains.Annotations;

using UnityEngine;

using Zenject;

using Zilon.Core.Client;
using Zilon.Core.Commands;
using Zilon.Core.Common;
using Zilon.Core.Tactics;
using Zilon.Core.Tactics.Spatial;

public class KeyboardMoveController : MonoBehaviour
{
    public SectorVM SectorViewModel;

    private readonly Dictionary<StepDirection, int> _stepDirectionIndexes = new Dictionary<StepDirection, int> {
        { StepDirection.Left, 0 },
        { StepDirection.LeftTop, 1 },
        { StepDirection.RightTop, 2 },
        { StepDirection.Right, 3 },
        { StepDirection.RightBottom, 4 },
        { StepDirection.LeftBottom, 5 }
    };

    private readonly Dictionary<KeyCode, StepDirection> _directionMap = new Dictionary<KeyCode, StepDirection> {
        { KeyCode.Keypad4, StepDirection.Left },
        { KeyCode.Keypad7, StepDirection.LeftTop },
        { KeyCode.Keypad9, StepDirection.RightTop },
        { KeyCode.Keypad6, StepDirection.Right },
        { KeyCode.Keypad3, StepDirection.RightBottom },
        { KeyCode.Keypad1, StepDirection.LeftBottom }
    };

    [Inject]
    private readonly ISectorUiState _sectorUiState;

    [Inject]
    private readonly ISectorManager _sectorManager;

    [Inject]
    private readonly ICommandBlockerService _commandBlockerService;

    [NotNull]
    [Inject]
    private readonly ICommandManager _clientCommandExecutor;

    [NotNull]
    [Inject(Id = "move-command")]
    private readonly ICommand _moveCommand;

    // Update is called once per frame
    void Update()
    {
        if (!_commandBlockerService.HasBlockers)
        {
            var direction = GetDirectionByKeyboard();

            if (direction == StepDirection.Undefined)
            {
                return;
            }

            var targetNode = GetTargetNode(direction);

            var targetNodeViewModel = SectorViewModel.NodeViewModels.SingleOrDefault(x => ReferenceEquals(x.Node, targetNode));

            _sectorUiState.SelectedViewModel = targetNodeViewModel;

            if (targetNodeViewModel != null)
            {
                _clientCommandExecutor.Push(_moveCommand);
            }
        }
    }

    private StepDirection GetDirectionByKeyboard()
    {
        var pressedKeyCode = DetectPressedKeyOrButton();
        if (_directionMap.TryGetValue(pressedKeyCode, out var stepDirection))
        {
            return stepDirection;
        }

        return StepDirection.Undefined;
    }

    private static KeyCode DetectPressedKeyOrButton()
    {
        foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(kcode))
                return kcode;


        }

        return KeyCode.None;
    }

    private IMapNode GetTargetNode(StepDirection direction)
    {
        if (direction == StepDirection.Undefined)
        {
            throw new ArgumentException("Не определено направление.", nameof(direction));
        }

        var node = _sectorUiState.ActiveActor.Actor.Node as HexNode;

        var neighborNodes = _sectorManager.CurrentSector.Map.GetNext(node).OfType<HexNode>();
        var directions = HexHelper.GetOffsetClockwise();

        var stepDirectionIndex = _stepDirectionIndexes[direction];
        var targetCubeCoords = node.CubeCoords + directions[stepDirectionIndex];

        var targetNode = neighborNodes.SingleOrDefault(x => x.CubeCoords == targetCubeCoords);
        return targetNode;
    }
}
