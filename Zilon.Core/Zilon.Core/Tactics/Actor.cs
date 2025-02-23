﻿using System;
using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;
using Zilon.Core.Common;
using Zilon.Core.Components;
using Zilon.Core.Persons;
using Zilon.Core.Players;
using Zilon.Core.Props;
using Zilon.Core.Schemes;
using Zilon.Core.Tactics.Behaviour;
using Zilon.Core.Tactics.Spatial;

namespace Zilon.Core.Tactics
{
    public sealed class Actor : IActor
    {
        private readonly IPerkResolver _perkResolver;

        public event EventHandler Moved;
        public event EventHandler<OpenContainerEventArgs> OpenedContainer;
        public event EventHandler<UsedActEventArgs> UsedAct;
        public event EventHandler<DamageTakenEventArgs> DamageTaken;

        /// <inheritdoc />
        /// <summary>
        /// Песонаж, который лежит в основе актёра.
        /// </summary>
        public IPerson Person { get; }

        /// <summary>
        /// Текущий узел карты, в котором находится актёр.
        /// </summary>
        public IMapNode Node { get; private set; }

        public IPlayer Owner { get; }

        [ExcludeFromCodeCoverage]
        public Actor([NotNull] IPerson person, [NotNull]  IPlayer owner, [NotNull]  IMapNode node)
        {
            Person = person ?? throw new ArgumentNullException(nameof(person));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }

        public Actor([NotNull] IPerson person, [NotNull]  IPlayer owner, [NotNull]  IMapNode node,
            [CanBeNull] IPerkResolver perkResolver) : this(person, owner, node)
        {
            _perkResolver = perkResolver;
        }

        public bool CanBeDamaged()
        {
            if (Person.Survival == null)
            {
                return false;
            }

            return !Person.Survival.IsDead;
        }

        public void MoveToNode(IMapNode targetNode)
        {
            Node = targetNode;
            Moved?.Invoke(this, new EventArgs());
        }

        public void OpenContainer(IPropContainer container, IOpenContainerMethod method)
        {
            var openResult = method.TryOpen(container);

            DoOpenContainer(container, openResult);
        }

        public void UseAct(IAttackTarget target, ITacticalAct tacticalAct)
        {
            DoUseAct(target, tacticalAct);
        }

        public void UseProp(IProp usedProp)
        {
            var useData = usedProp.Scheme.Use;

            foreach (var rule in useData.CommonRules)
            {
                switch (rule.Direction)
                {
                    case PersonRuleDirection.Positive:
                        switch (rule.Type)
                        {
                            case ConsumeCommonRuleType.Satiety:
                                RestoreStat(SurvivalStatType.Satiety, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Thirst:
                                RestoreStat(SurvivalStatType.Hydration, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Health:
                                RestoreStat(SurvivalStatType.Health, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Intoxication:
                                RiseStat(SurvivalStatType.Intoxication, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Undefined:
                            default:
                                throw new ArgumentOutOfRangeException($"Правило поглощения {rule.Type} не поддерживается.");
                        }
                        break;
                    case PersonRuleDirection.Negative:
                        switch (rule.Type)
                        {
                            case ConsumeCommonRuleType.Satiety:
                                DecreaseStat(SurvivalStatType.Satiety, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Thirst:
                                DecreaseStat(SurvivalStatType.Hydration, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Health:
                                DecreaseStat(SurvivalStatType.Health, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Intoxication:
                                DecreaseStat(SurvivalStatType.Intoxication, rule.Level);
                                break;

                            case ConsumeCommonRuleType.Undefined:
                            default:
                                throw new ArgumentOutOfRangeException($"Правило поглощения {rule.Type} не поддерживается.");
                        }
                        break;
                }

            }

            if (useData.Consumable)
            {
                ConsumeResource(usedProp);

                if (_perkResolver != null)
                {
                    var consumeProgress = new ConsumeProviantJobProgress();
                    _perkResolver.ApplyProgress(consumeProgress, Person.EvolutionData);
                }
            }
        }

        private void ConsumeResource(IProp usedProp)
        {
            switch (usedProp)
            {
                case Resource resource:
                    var removeResource = new Resource(resource.Scheme, 1);
                    Person.Inventory.Remove(removeResource);
                    break;
            }
        }

        public void TakeDamage(int value)
        {
            Person.Survival?.DecreaseStat(SurvivalStatType.Health, value);

            if (_perkResolver != null && Person.EvolutionData != null)
            {
                var takeDamageProgress = new TakeDamageJobProgress(value);
                _perkResolver.ApplyProgress(takeDamageProgress, Person.EvolutionData);

                var takeHitProgress = new TakeHitJobProgress();
                _perkResolver.ApplyProgress(takeHitProgress, Person.EvolutionData);
            }

            DoDamageTaken(value);
        }

        [ExcludeFromCodeCoverage]
        private void DoDamageTaken(int value)
        {
            DamageTaken?.Invoke(this, new DamageTakenEventArgs(value));
        }


        [ExcludeFromCodeCoverage]
        private void DoOpenContainer(IPropContainer container, IOpenContainerResult openResult)
        {
            var e = new OpenContainerEventArgs(container, openResult);
            OpenedContainer?.Invoke(this, e);
        }

        [ExcludeFromCodeCoverage]
        private void DoUseAct(IAttackTarget target, ITacticalAct tacticalAct)
        {
            var args = new UsedActEventArgs(target, tacticalAct);
            UsedAct?.Invoke(this, args);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"{Person}";
        }

        /// <summary>
        /// Метод введён специально для повышения уровня интоксикации.
        /// Так как глупо выглядит ResToreStat для повышения интоксикации.
        /// Просто семантически более удобная обёртка.
        /// </summary>
        /// <param name="statType"> Характеристика, повышаемая методом. </param>
        /// <param name="level"> Уровень увеличения. </param>
        private void RiseStat(SurvivalStatType statType, PersonRuleLevel level)
        {
            RestoreStat(statType, level);
        }

        private void RestoreStat(SurvivalStatType statType, PersonRuleLevel level)
        {
            switch (statType)
            {
                case SurvivalStatType.Satiety:
                case SurvivalStatType.Hydration:
                    RestoreSurvivalStatInner(statType, level);
                    break;

                case SurvivalStatType.Intoxication:
                    RiseIntoxicationLevel(level);
                    break;

                case SurvivalStatType.Health:
                    RestoreHp(level);
                    break;
            }
        }

        private void RestoreSurvivalStatInner(SurvivalStatType statType, PersonRuleLevel level)
        {
            switch (level)
            {
                case PersonRuleLevel.Lesser:
                    Person.Survival?.RestoreStat(statType,
                        PropMetrics.SurvivalLesserRestoreValue + 1);
                    break;

                case PersonRuleLevel.Normal:
                    Person.Survival?.RestoreStat(statType,
                        PropMetrics.SurvivalNormalRestoreValue + 1);
                    break;

                case PersonRuleLevel.Grand:
                    Person.Survival?.RestoreStat(statType,
                        PropMetrics.SurvivalGrandRestoreValue + 1);
                    break;

                case PersonRuleLevel.None:
                    throw new NotSupportedException();

                case PersonRuleLevel.Absolute:
                    throw new NotSupportedException();

                default:
                    throw new InvalidOperationException($"Неизвестный уровень влияния правила {level}.");
            }
        }

        private void RestoreHp(PersonRuleLevel level)
        {
            switch (level)
            {
                case PersonRuleLevel.Lesser:
                    Person.Survival?.RestoreStat(SurvivalStatType.Health,
                        PropMetrics.HpLesserRestoreValue);
                    break;

                case PersonRuleLevel.Normal:
                    Person.Survival?.RestoreStat(SurvivalStatType.Health,
                        PropMetrics.HpNormalRestoreValue);
                    break;

                case PersonRuleLevel.Grand:
                    Person.Survival?.RestoreStat(SurvivalStatType.Health,
                        PropMetrics.HpGrandRestoreValue);
                    break;

                default:
                    throw new InvalidOperationException($"Неизвестный уровень влияния правила {level}.");
            }
        }

        private void RiseIntoxicationLevel(PersonRuleLevel level)
        {
            switch (level)
            {
                case PersonRuleLevel.Lesser:
                    Person.Survival?.RestoreStat(SurvivalStatType.Intoxication,
                        PropMetrics.INTOXICATION_RISE_LESSER_VALUE + 1);
                    break;

                case PersonRuleLevel.Normal:
                    Person.Survival?.RestoreStat(SurvivalStatType.Intoxication,
                        PropMetrics.INTOXICATION_RISE_NORMAL_VALUE + 1);
                    break;

                case PersonRuleLevel.Grand:
                    Person.Survival?.RestoreStat(SurvivalStatType.Intoxication,
                        PropMetrics.INTOXICATION_RISE_GRAND_VALUE + 1);
                    break;

                default:
                    throw new InvalidOperationException($"Неизвестный уровень влияния правила {level}.");
            }
        }

        private void DecreaseStat(SurvivalStatType statType, PersonRuleLevel level)
        {
            switch (statType)
            {
                case SurvivalStatType.Satiety:
                case SurvivalStatType.Hydration:
                case SurvivalStatType.Intoxication:
                    DecreaseSurvivalStatInner(statType, level);
                    break;

                case SurvivalStatType.Health:
                    DecreaseHp(level);
                    break;
            }
        }

        private void DecreaseSurvivalStatInner(SurvivalStatType statType, PersonRuleLevel level)
        {
            switch (level)
            {
                case PersonRuleLevel.Lesser:
                    Person.Survival?.DecreaseStat(statType,
                        PropMetrics.SurvivalLesserRestoreValue - 1);
                    break;

                case PersonRuleLevel.Normal:
                    Person.Survival?.DecreaseStat(statType,
                        PropMetrics.SurvivalNormalRestoreValue - 1);
                    break;

                case PersonRuleLevel.Grand:
                    Person.Survival?.DecreaseStat(statType,
                        PropMetrics.SurvivalGrandRestoreValue - 1);
                    break;

                case PersonRuleLevel.None:
                    throw new NotSupportedException();

                case PersonRuleLevel.Absolute:
                    throw new NotSupportedException();

                default:
                    throw new InvalidOperationException($"Неизвестный уровень влияния правила {level}.");
            }
        }

        private void DecreaseHp(PersonRuleLevel level)
        {
            switch (level)
            {
                case PersonRuleLevel.Lesser:
                    Person.Survival?.DecreaseStat(SurvivalStatType.Health,
                        PropMetrics.HpLesserRestoreValue);
                    break;

                case PersonRuleLevel.Normal:
                    Person.Survival?.DecreaseStat(SurvivalStatType.Health,
                        PropMetrics.HpNormalRestoreValue);
                    break;

                case PersonRuleLevel.Grand:
                    Person.Survival?.DecreaseStat(SurvivalStatType.Health,
                        PropMetrics.HpGrandRestoreValue);
                    break;

                default:
                    throw new InvalidOperationException($"Неизвестный уровень влияния правила {level}.");
            }
        }
    }
}
