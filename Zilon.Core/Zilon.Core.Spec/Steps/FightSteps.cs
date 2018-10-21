﻿using System.Linq;
using FluentAssertions;

using LightInject;

using TechTalk.SpecFlow;

using Zilon.Core.Client;
using Zilon.Core.Commands;
using Zilon.Core.Persons;
using Zilon.Core.Spec.Contexts;
using Zilon.Core.Tactics;
using Zilon.Core.Tests.Common;

namespace Zilon.Core.Spec.Steps
{
    [Binding]
    public sealed class FightSteps : GenericStepsBase<CommonGameActionsContext>
    {
        public FightSteps(CommonGameActionsContext context) : base(context)
        {
        }

        [When(@"Актёр игрока атакует монстра Id:(.*)")]
        public void WhenАктёрИгрокаАтакуетМонстраId(int monsterId)
        {
            var attackCommand = Context.Container.GetInstance<ICommand>("attack");
            var playerState = Context.Container.GetInstance<IPlayerState>();

            var monster = Context.GetMonsterById(monsterId);

            var monsterViewModel = new TestActorViewModel
            {
                Actor = monster
            };

            playerState.HoverViewModel = monsterViewModel;

            attackCommand.Execute();
        }

        [Then(@"Актёр игрока мертв")]
        public void ThenАктёрИгрокаМертв()
        {
            var actor = Context.GetActiveActor();

            actor.Person.Survival.IsDead.Should().BeTrue();
        }

        [Then(@"Монстр Id:(.*) успешно обороняется")]
        public void ThenМонстрIdУспешноОбороняется(int monsterId)
        {
            var visual = Context.VisualEvents.Last();

            visual.EventName.Should().Be(nameof(IActor.OnDefence));

            var monster = Context.GetMonsterById(monsterId);
            visual.Actor.Should().BeSameAs(monster);
        }

        [Then(@"Тактическое умение (.*) имеет дебафф на эффективность")]
        public void ThenТактическоеУмениеChopИмеетДебаффНаЭффективность(string tacticalActSid)
        {
            var actor = Context.GetActiveActor();

            var act = actor.Person.TacticalActCarrier.Acts.OfType<TacticalAct>()
                .Single(x => x.Scheme.Sid == tacticalActSid);

            act.Efficient.Modifiers.ResultBuff.Should().Be(-1);
        }

    }
}
