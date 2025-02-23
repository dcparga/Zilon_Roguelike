﻿using System;
using System.Collections.Generic;
using System.Linq;

using Zilon.Core.Components;
using Zilon.Core.Persons;
using Zilon.Core.Props;
using Zilon.Core.Schemes;
using Zilon.Core.Tactics.ActorInteractionEvents;
using Zilon.Core.Tactics.Spatial;

namespace Zilon.Core.Tactics
{
    /// <summary>
    /// Базовая реализация сервиса для работы с действиями персонажа.
    /// </summary>
    /// <seealso cref="Zilon.Core.Tactics.ITacticalActUsageService" />
    public sealed class TacticalActUsageService : ITacticalActUsageService
    {
        private readonly ITacticalActUsageRandomSource _actUsageRandomSource;
        private readonly IPerkResolver _perkResolver;
        private readonly ISectorManager _sectorManager;

        /// <summary>Сервис для работы с прочностью экипировки.</summary>
        public IEquipmentDurableService EquipmentDurableService { get; set; }

        /// <summary>
        /// Шина событий возаимодействия актёров.
        /// </summary>
        public IActorInteractionBus ActorInteractionBus { get; set; }

        /// <summary>
        /// Конструирует экземпляр службы <see cref="TacticalActUsageService"/>.
        /// </summary>
        /// <param name="actUsageRandomSource">Источник рандома для выполнения действий.</param>
        /// <param name="perkResolver">Сервис для работы с прогрессом перков.</param>
        /// <param name="sectorManager">Менеджер сектора.</param>
        /// <exception cref="System.ArgumentNullException">
        /// actUsageRandomSource
        /// or
        /// perkResolver
        /// or
        /// sectorManager
        /// </exception>
        public TacticalActUsageService(ITacticalActUsageRandomSource actUsageRandomSource,
            IPerkResolver perkResolver,
            ISectorManager sectorManager)
        {
            _actUsageRandomSource = actUsageRandomSource ?? throw new ArgumentNullException(nameof(actUsageRandomSource));
            _perkResolver = perkResolver ?? throw new ArgumentNullException(nameof(perkResolver));
            _sectorManager = sectorManager ?? throw new ArgumentNullException(nameof(sectorManager));
        }

        public void UseOn(IActor actor, IAttackTarget target, UsedTacticalActs usedActs)
        {
            foreach (var act in usedActs.Primary)
            {
                if (!act.Stats.Targets.HasFlag(TacticalActTargets.Self) && actor == target)
                {
                    throw new ArgumentException("Актёр не может атаковать сам себя", nameof(target));
                }

                UseAct(actor, target, act);
            }

            // Использование дополнительных действий.
            // Используются с некоторой вероятностью.
            foreach (var act in usedActs.Secondary)
            {
                var useSuccessRoll = GetUseSuccessRoll();
                var useFactRoll = GetUseFactRoll();

                if (useFactRoll < useSuccessRoll)
                {
                    continue;
                }

                UseAct(actor, target, act);
            }
        }

        private void UseAct(IActor actor, IAttackTarget target, ITacticalAct act)
        {
            bool isInDistance;
            if ((act.Stats.Targets & TacticalActTargets.Self) > 0 && actor == target)
            {
                isInDistance = true;
            }
            else
            {
                var currentCubePos = ((HexNode)actor.Node).CubeCoords;
                var targetCubePos = ((HexNode)target.Node).CubeCoords;

                isInDistance = act.CheckDistance(currentCubePos, targetCubePos);
            }

            if (!isInDistance)
            {
                throw new InvalidOperationException("Попытка атаковать цель, находящуюся за пределами атаки.");
            }

            var targetNode = target.Node;

            var targetIsOnLine = _sectorManager.CurrentSector.Map.TargetIsOnLine(
                actor.Node,
                targetNode);

            if (!targetIsOnLine)
            {
                throw new InvalidOperationException("Задачу на атаку нельзя выполнить сквозь стены.");
            }


            actor.UseAct(target, act);

            var tacticalActRoll = GetActEfficient(act);

            // Изъятие патронов
            if (act.Constrains?.PropResourceType != null)
            {
                RemovePropResource(actor, act);
            }

            if (target is IActor targetActor)
            {
                UseOnActor(actor, targetActor, tacticalActRoll);
            }
            else
            {
                UseOnChest(target, tacticalActRoll);
            }

            if (act.Equipment != null)
            {
                EquipmentDurableService?.UpdateByUse(act.Equipment, actor.Person);
            }
        }

        private static void RemovePropResource(IActor actor, ITacticalAct act)
        {
            var propResources = from prop in actor.Person.Inventory.CalcActualItems()
                                where prop is Resource
                                where prop.Scheme.Bullet?.Caliber == act.Constrains.PropResourceType
                                select prop;

            if (propResources.FirstOrDefault() is Resource propResource)
            {
                if (propResource.Count >= act.Constrains.PropResourceCount)
                {
                    var usedResource = new Resource(propResource.Scheme, act.Constrains.PropResourceCount.Value);
                    actor.Person.Inventory.Remove(usedResource);
                }
                else
                {
                    throw new InvalidOperationException($"Не хватает ресурса {propResource} для использования действия {act}.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Не хватает ресурса {act.Constrains?.PropResourceType} для использования действия {act}.");
            }
        }

        private int GetUseFactRoll()
        {
            var roll = _actUsageRandomSource.RollUseSecondaryAct();
            return roll;
        }

        private int GetUseSuccessRoll()
        {
            // В будущем успех использования вторичных дейсвий будет зависить от действия, экипировки, перков.
            return 5;
        }

        /// <summary>
        /// Возвращает случайное значение эффективность действия.
        /// </summary>
        /// <param name="act"> Соверщённое действие. </param>
        /// <returns> Возвращает выпавшее значение эффективности. </returns>
        private TacticalActRoll GetActEfficient(ITacticalAct act)
        {
            var rolledEfficient = _actUsageRandomSource.RollEfficient(act.Efficient);

            var roll = new TacticalActRoll(act, rolledEfficient);

            return roll;
        }

        /// <summary>
        /// Применяет действие на предмет, на который можно подействовать (сундук/дверь).
        /// </summary>
        /// <param name="target"> Цель использования действия. </param>
        /// <param name="tacticalActRoll"> Эффективность действия. </param>
        private static void UseOnChest(IAttackTarget target, TacticalActRoll tacticalActRoll)
        {
            target.TakeDamage(tacticalActRoll.Efficient);
        }

        /// <summary>
        /// Применяет действие на актёра.
        /// </summary>
        /// <param name="actor"> Актёр, который совершил действие. </param>
        /// <param name="targetActor"> Цель использования действия. </param>
        /// <param name="tacticalActRoll"> Эффективность действия. </param>
        private void UseOnActor(IActor actor, IActor targetActor, TacticalActRoll tacticalActRoll)
        {
            switch (tacticalActRoll.TacticalAct.Stats.Effect)
            {
                case TacticalActEffectType.Damage:
                    DamageActor(actor, targetActor, tacticalActRoll);
                    break;

                case TacticalActEffectType.Heal:
                    HealActor(actor, targetActor, tacticalActRoll);
                    break;

                default:
                    throw new ArgumentException(string.Format("Не определённый эффект {0} действия {1}.",
                        tacticalActRoll.TacticalAct.Stats.Effect,
                        tacticalActRoll.TacticalAct));
            }
        }

        /// <summary>
        /// Производит попытку нанесения урона целевову актёру с учётом обороны и брони.
        /// </summary>
        /// <param name="actor"> Актёр, который совершил действие. </param>
        /// <param name="targetActor"> Цель использования действия. </param>
        /// <param name="tacticalActRoll"> Эффективность действия. </param>
        private void DamageActor(IActor actor, IActor targetActor, TacticalActRoll tacticalActRoll)
        {
            var targetIsDeadLast = targetActor.Person.CheckIsDead();

            var offenceType = tacticalActRoll.TacticalAct.Stats.Offence.Type;
            var usedDefences = GetCurrentDefences(targetActor, offenceType);

            var prefferedDefenceItem = HitHelper.CalcPreferredDefense(usedDefences);
            var successToHitRoll = HitHelper.CalcSuccessToHit(prefferedDefenceItem);
            var factToHitRoll = _actUsageRandomSource.RollToHit(tacticalActRoll.TacticalAct.ToHit);

            if (factToHitRoll >= successToHitRoll)
            {
                var damageEfficientCalcResult = CalcEfficient(targetActor, tacticalActRoll);
                var actEfficient = damageEfficientCalcResult.ResultEfficient;

                ProcessSuccessfulAttackEvent(
                    actor,
                    targetActor,
                    damageEfficientCalcResult,
                    successToHitRoll,
                    factToHitRoll);

                if (actEfficient <= 0)
                {
                    return;
                }

                targetActor.TakeDamage(actEfficient);

                CountTargetActorAttack(actor, targetActor, tacticalActRoll.TacticalAct);

                if (EquipmentDurableService != null && targetActor.Person.EquipmentCarrier != null)
                {
                    var damagedEquipment = GetDamagedEquipment(targetActor);

                    // может быть null, если нет брони вообще
                    if (damagedEquipment != null)
                    {
                        EquipmentDurableService.UpdateByUse(damagedEquipment, targetActor.Person);
                    }
                }

                if (!targetIsDeadLast && targetActor.Person.CheckIsDead())
                {
                    CountTargetActorDefeat(actor, targetActor);
                }
            }
            else
            {
                if (prefferedDefenceItem != null)
                {
                    // Это промах, потому что целевой актёр увернулся.
                    ProcessAttackDodgeEvent(actor,
                        targetActor,
                        prefferedDefenceItem,
                        successToHitRoll,
                        factToHitRoll);
                }
                else
                {
                    // Это промах чистой воды.
                    ProcessPureMissEvent(actor,
                        targetActor,
                        successToHitRoll,
                        factToHitRoll);
                }
            }
        }

        private void ProcessSuccessfulAttackEvent(
            IActor actor,
            IActor targetActor,
            DamageEfficientCalc damageEfficientCalcResult,
            int successToHitRoll,
            int factToHitRoll)
        {
            if (ActorInteractionBus == null)
            {
                return;
            }

            var damageEvent = new DamageActorInteractionEvent(actor, targetActor, damageEfficientCalcResult)
            {
                SuccessToHitRoll = successToHitRoll,
                FactToHitRoll = factToHitRoll
            };
            ActorInteractionBus.PushEvent(damageEvent);
        }

        private void ProcessPureMissEvent(IActor actor, IActor targetActor, int successToHitRoll, int factToHitRoll)
        {
            if (ActorInteractionBus == null)
            {
                return;
            }

            var damageEvent = new PureMissActorInteractionEvent(actor, targetActor)
            {
                SuccessToHitRoll = successToHitRoll,
                FactToHitRoll = factToHitRoll
            };

            ActorInteractionBus.PushEvent(damageEvent);
        }

        private void ProcessAttackDodgeEvent(
            IActor actor,
            IActor targetActor,
            PersonDefenceItem personDefenceItem,
            int successToHitRoll,
            int factToHitRoll)
        {
            if (ActorInteractionBus == null)
            {
                return;
            }

            var interactEvent = new DodgeActorInteractionEvent(actor, targetActor, personDefenceItem)
            {
                SuccessToHitRoll = successToHitRoll,
                FactToHitRoll = factToHitRoll
            };

            ActorInteractionBus.PushEvent(interactEvent);
        }

        private void CountTargetActorAttack(IActor actor, IActor targetActor, ITacticalAct tacticalAct)
        {
            if (actor.Person is MonsterPerson)
            {
                // Монстры не могут прокачиваться.
                return;
            }

            if (actor.Person == null)
            {
                // Это может происходить в тестах,
                // если в моках не определили персонажа.
                //TODO Поискать решение, как всегда быть уверенным, что персонаж указан в боевых условиях, и может быть null в тестах.
                //TODO Эта же проверка нужна в CountActorDefeat (учёт убиства актёра).
                return;
            }

            var evolutionData = actor.Person.EvolutionData;

            //TODO Такую же проверку добавить в CountActorDefeat (учёт убиства актёра).
            if (evolutionData == null)
            {
                return;
            }

            var progress = new AttackActorJobProgress(targetActor, tacticalAct);

            _perkResolver.ApplyProgress(progress, evolutionData);
        }

        private Equipment GetDamagedEquipment(IActor targetActor)
        {
            if (targetActor.Person.EquipmentCarrier == null)
            {
                throw new ArgumentException("Передан персонаж, который не может носить экипировку.");
            }

            var armorEquipments = new List<Equipment>();
            foreach (var currentEquipment in targetActor.Person.EquipmentCarrier)
            {
                if (currentEquipment == null)
                {
                    continue;
                }

                if (currentEquipment.Scheme.Equip?.Armors != null)
                {
                    armorEquipments.Add(currentEquipment);
                }
            }

            var rolledDamagedEquipment = _actUsageRandomSource.RollDamagedEquipment(armorEquipments);

            return rolledDamagedEquipment;
        }

        /// <summary>
        /// Лечит актёра.
        /// </summary>
        /// <param name="actor"> Актёр, который совершил действие. </param>
        /// <param name="targetActor"> Цель использования действия. </param>
        /// <param name="tacticalActRoll"> Эффективность действия. </param>
        private void HealActor(IActor actor, IActor targetActor, TacticalActRoll tacticalActRoll)
        {
            targetActor.Person.Survival?.RestoreStat(SurvivalStatType.Health, tacticalActRoll.Efficient);
        }

        /// <summary>
        /// Расчитывает эффективность умения с учётом поглощения броней.
        /// </summary>
        /// <param name="targetActor"> Целевой актёр. </param>
        /// <param name="tacticalActRoll"> Результат броска исходной эфективности действия. </param>
        /// <returns> Возвращает числовое значение эффективности действия. </returns>
        private DamageEfficientCalc CalcEfficient(IActor targetActor, TacticalActRoll tacticalActRoll)
        {
            var damageEfficientCalcResult = new DamageEfficientCalc();

            var actApRank = GetActApRank(tacticalActRoll.TacticalAct); ;
            damageEfficientCalcResult.ActApRank = actApRank;
            var armorRank = GetArmorRank(targetActor, tacticalActRoll.TacticalAct);
            damageEfficientCalcResult.ArmorRank = armorRank;

            var actEfficientArmorBlocked = tacticalActRoll.Efficient;
            var rankDiff = actApRank - armorRank;

            if (armorRank != null && rankDiff < 10)
            {
                var factArmorSaveRoll = RollArmorSave();
                damageEfficientCalcResult.FactArmorSaveRoll = factArmorSaveRoll;
                var successArmorSaveRoll = GetSuccessArmorSave(targetActor, tacticalActRoll.TacticalAct);
                damageEfficientCalcResult.SuccessArmorSaveRoll = successArmorSaveRoll;
                if (factArmorSaveRoll >= successArmorSaveRoll)
                {
                    damageEfficientCalcResult.TargetSuccessfullUsedArmor = true;
                    var armorAbsorbtion = GetArmorAbsorbtion(targetActor, tacticalActRoll.TacticalAct);
                    damageEfficientCalcResult.ArmorAbsorbtion = armorAbsorbtion;
                    actEfficientArmorBlocked = AbsorbActEfficient(actEfficientArmorBlocked, armorAbsorbtion);
                }
            }

            damageEfficientCalcResult.ActEfficientArmorBlocked = actEfficientArmorBlocked;

            return damageEfficientCalcResult;
        }

        /// <summary>
        /// Извлечение всех оборон актёра, способных противостоять указанному типу урона.
        /// Включая DivineDefence, противодействующий всем типам урона.
        /// </summary>
        /// <param name="targetActor"> Целевой актёр. </param>
        /// <param name="offenceType"> Тип урона. </param>
        /// <returns> Возвращает набор оборон. </returns>
        private static IEnumerable<PersonDefenceItem> GetCurrentDefences(IActor targetActor, OffenseType offenceType)
        {
            var defenceType = HitHelper.GetDefence(offenceType);

            return targetActor.Person.CombatStats.DefenceStats.Defences
                            .Where(x => x.Type == defenceType || x.Type == DefenceType.DivineDefence);
        }

        /// <summary>
        /// Расчёт эффективности умения с учётом поглащения бронёй.
        /// </summary>
        /// <param name="efficient"> Эффективность умения. </param>
        /// <param name="armorAbsorbtion"> Числовое значение поглощения брони. </param>
        /// <returns> Возвращает поглощённое значение эффективности. Эффективность не может быть меньше нуля при поглощении. </returns>
        private static int AbsorbActEfficient(int efficient, int armorAbsorbtion)
        {
            efficient -= armorAbsorbtion;

            if (efficient < 0)
            {
                efficient = 0;
            }

            return efficient;
        }

        /// <summary>
        /// Возвращает показатель поглощения брони цели.
        /// Это величина, на которую будет снижен урон.
        /// </summary>
        /// <param name="targetActor"> Целевой актёр, для которого проверяется поглощение урона. </param>
        /// <param name="usedTacticalAct"> Действие, которое будет использовано для нанесения урона. </param>
        /// <returns> Возвращает показатель поглощения брони цели. </returns>
        private static int GetArmorAbsorbtion(IActor targetActor, ITacticalAct usedTacticalAct)
        {
            var actorArmors = targetActor.Person.CombatStats.DefenceStats.Armors;
            var actImpact = usedTacticalAct.Stats.Offence.Impact;
            var preferredArmor = actorArmors.FirstOrDefault(x => x.Impact == actImpact);

            if (preferredArmor == null)
            {
                return 0;
            }

            switch (preferredArmor.AbsorbtionLevel)
            {
                case PersonRuleLevel.None:
                    return 0;

                case PersonRuleLevel.Lesser:
                    return 1;

                case PersonRuleLevel.Normal:
                    return 2;

                case PersonRuleLevel.Grand:
                    return 5;

                case PersonRuleLevel.Absolute:
                    return 10;

                default:
                    throw new InvalidOperationException($"Неизвестный уровень поглощения брони {preferredArmor.AbsorbtionLevel}.");
            }
        }

        /// <summary>
        /// Рассчитывает успешный спас-бросок за броню цели.
        /// </summary>
        /// <param name="targetActor"> Целевой актёр, для которого проверяется спас-бросок за броню. </param>
        /// <param name="usedTacticalAct"> Действие, для которого будет проверятся спас-бросок за броню. </param>
        /// <returns> Величина успешного спас-броска за броню. </returns>
        /// <remarks>
        /// При равных рангах броня пробивается на 4+.
        /// За каждые два ранга превосходства действия над бронёй - увеличение на 1.
        /// </remarks>
        private static int GetSuccessArmorSave(IActor targetActor, ITacticalAct usedTacticalAct)
        {
            var actorArmors = targetActor.Person.CombatStats.DefenceStats.Armors;
            var actImpact = usedTacticalAct.Stats.Offence.Impact;
            var preferredArmor = actorArmors.FirstOrDefault(x => x.Impact == actImpact);

            if (preferredArmor == null)
            {
                throw new InvalidOperationException($"Не найдена защита {actImpact}.");
            }

            var apRankDiff = usedTacticalAct.Stats.Offence.ApRank - preferredArmor.ArmorRank;

            switch (apRankDiff)
            {
                case 1:
                case 0:
                case -1:
                    return 4;

                case 2:
                case 3:
                    return 5;

                case 4:
                case 5:
                case 6:
                    return 6;

                case -2:
                case -3:
                    return 3;

                case -4:
                case -5:
                case -6:
                    return 2;

                default:
                    if (apRankDiff >= 7)
                    {
                        return 7;
                    }
                    else if (apRankDiff <= -7)
                    {
                        return 1;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
            }
        }

        /// <summary>
        /// Возвращает результат спас-броска на броню.
        /// </summary>
        /// <returns></returns>
        private int RollArmorSave()
        {
            var factRoll = _actUsageRandomSource.RollArmorSave();
            return factRoll;
        }

        /// <summary>
        /// Возвращает ранг брони цели.
        /// </summary>
        /// <param name="targetActor"> Актёр, для которого выбирается ранг брони. </param>
        /// <param name="usedTacticalAct"> Действие, от которого требуется броня. </param>
        /// <returns> Возвращает числовое значение ранга брони указанного типа. </returns>
        private static int? GetArmorRank(IActor targetActor, ITacticalAct usedTacticalAct)
        {
            var actorArmors = targetActor.Person.CombatStats.DefenceStats.Armors;
            var actImpact = usedTacticalAct.Stats.Offence.Impact;
            var preferredArmor = actorArmors.FirstOrDefault(x => x.Impact == actImpact);

            return preferredArmor?.ArmorRank;
        }

        /// <summary>
        /// Возвращает ранг пробития действия.
        /// </summary>
        /// <param name="tacticalAct"></param>
        /// <returns></returns>
        private int GetActApRank(ITacticalAct tacticalAct)
        {
            return tacticalAct.Stats.Offence.ApRank;
        }

        /// <summary>
        /// Расчитывает убийство целевого актёра.
        /// </summary>
        /// <param name="actor"> Актёр, который совершил действие. </param>
        /// <param name="targetActor"> Цель использования действия. </param>
        private void CountTargetActorDefeat(IActor actor, IActor targetActor)
        {
            if (actor.Person is MonsterPerson)
            {
                // Монстры не могут прокачиваться.
                return;
            }

            var evolutionData = actor.Person.EvolutionData;

            var defeatProgress = new DefeatActorJobProgress(targetActor);

            _perkResolver.ApplyProgress(defeatProgress, evolutionData);
        }
    }
}
