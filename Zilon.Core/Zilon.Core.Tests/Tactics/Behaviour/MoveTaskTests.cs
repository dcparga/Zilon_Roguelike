﻿using System.Collections.Generic;
using System.Linq;

using FluentAssertions;
using Moq;
using NUnit.Framework;

using Zilon.Core.Persons;
using Zilon.Core.Players;
using Zilon.Core.Tactics.Spatial;
using Zilon.Core.Tests.TestCommon;

namespace Zilon.Core.Tactics.Behaviour.Tests
{
    [TestFixture]
    public class MoveTaskTests
    {
        /// <summary>
        /// Тест проверяет, то задача на перемещение за несколько итераций
        /// перемещает актёра в целевой узел. В конце, на последней итерации,
        /// когда актёр достиг цели, должна отмечаться, как заверщённая.
        /// </summary>
        [Test]
        public void ExecuteTest_OpenGridMap_ActorReachPointAndTaskComplete()
        {
            // ARRANGE
            var map = new TestGridGenMap();

            var startNode = map.Nodes.Cast<HexNode>().SelectBy(3, 3);
            var finishNode = map.Nodes.Cast<HexNode>().SelectBy(1, 5);

            var expectedPath = new[] {
                map.Nodes.Cast<HexNode>().SelectBy(2, 3),
                map.Nodes.Cast<HexNode>().SelectBy(2, 4),
                finishNode
            };

            var actor = CreateActor(startNode);

            var task = new MoveTask(actor, finishNode, map);


            // ACT
            for (var step = 1; step <= expectedPath.Count(); step++)
            {
                task.Execute();



                // ASSERT
                if (step < 3)
                {
                    task.IsComplete.Should().Be(false);
                }
                else
                {
                    task.IsComplete.Should().Be(true);
                }

                actor.Node.Should().Be(expectedPath[step - 1]);
            }



            // ASSERT

            task.IsComplete.Should().Be(true);
            actor.Node.Should().Be(finishNode);
        }

        private static IActor CreateActor(HexNode startNode)
        {
            var playerMock = new Mock<IPlayer>();
            var player = playerMock.Object;

            var personMock = new Mock<IPerson>();
            var person = personMock.Object;

            IMapNode currentNode = startNode;
            var actorMock = new Mock<IActor>();
            actorMock.SetupGet(x => x.Node).Returns(() => currentNode);
            actorMock.Setup(x => x.MoveToNode(It.IsAny<IMapNode>()))
                .Callback<IMapNode>(node => currentNode = node);
            var actor = actorMock.Object;

            return new Actor(person, player, startNode);
        }

        /// <summary>
        /// Тест проверяет, что задача на перемещение учитывает стены.
        /// Актёр должен идти по пути, огибажщем стены.
        /// </summary>
        [Test]
        public void ExecuteTest_MapWithWalls_ActorAvoidWalls()
        {
            // ARRANGE

            var map = new TestGridGenWallMap();

            var expectedPath = new IMapNode[] {
                map.Nodes.Cast<HexNode>().SelectBy(4,4),
                map.Nodes.Cast<HexNode>().SelectBy(3,4),
                map.Nodes.Cast<HexNode>().SelectBy(2,4),
                map.Nodes.Cast<HexNode>().SelectBy(1,5),
            };

            var startNode = expectedPath.First();
            var finishNode = expectedPath.Last();


            var actor = CreateActor((HexNode)startNode);

            var task = new MoveTask(actor, finishNode, map);


            // ACT
            for (var step = 1; step < expectedPath.Length; step++)
            {
                task.Execute();


                // ASSERT
                actor.Node.Should().Be(expectedPath[step]);
            }
        }
    }
}