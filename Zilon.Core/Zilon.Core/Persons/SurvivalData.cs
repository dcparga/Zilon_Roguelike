﻿using System;
using System.Linq;

using Zilon.Core.Common;

namespace Zilon.Core.Persons
{
    /// <summary>
    /// Базовая реализация данных о выживании.
    /// </summary>
    public sealed class SurvivalData : ISurvivalData
    {
        public SurvivalData()
        {
            Stats = new[] {
                CreateStat(SurvivalStatType.Satiety),
                CreateStat(SurvivalStatType.Water)
            };
        }

        public SurvivalStat[] Stats { get; }
        public bool IsDead { get; private set; }

        public event EventHandler<SurvivalStatChangedEventArgs> StatCrossKeyValue;
        public event EventHandler Dead;

        public void RestoreStat(SurvivalStatType type, int value)
        {
            var stat = Stats.SingleOrDefault(x => x.Type == type);
            if (stat != null)
            {
                ChangeStatInner(stat, value);
            }
        }

        public void DecreaseStat(SurvivalStatType type, int value)
        {
            var stat = Stats.SingleOrDefault(x => x.Type == type);
            if (stat != null)
            {
                ChangeStatInner(stat, -value);
            }
        }

        public void SetStatForce(SurvivalStatType type, int value)
        {
            var stat = Stats.SingleOrDefault(x => x.Type == type);
            if (stat != null)
            {
                stat.Value = value;
            }
        }

        public void Update()
        {
            foreach (var stat in Stats)
            {
                ChangeStatInner(stat, -stat.Rate);
            }
        }

        private void ChangeStatInner(SurvivalStat stat, int value)
        {
            var oldValue = stat.Value;

            stat.Value += value;

            var diff = RangeHelper.CreateNormalized(oldValue, stat.Value);

            foreach (var keyPoint in stat.KeyPoints)
            {
                if (diff.Contains(keyPoint.Value))
                {
                    DoStatCrossKeyPoint(stat, keyPoint);
                }
            }

            CheckHp(stat);
        }

        private void CheckHp(SurvivalStat stat)
        {
            if (stat.Type == SurvivalStatType.Health)
            {
                var hp = stat.Value;
                if (hp <= 0)
                {
                    IsDead = true;
                    DoDead();
                }
            }
        }

        private void DoDead()
        {
            Dead?.Invoke(this, new EventArgs());
        }

        private static SurvivalStat CreateStat(SurvivalStatType type)
        {
            var stat = new SurvivalStat(50, -100, 100)
            {
                Type = type,
                Rate = 1,
                KeyPoints = new[]{
                        new SurvivalStatKeyPoint{
                            Level = SurvivalStatHazardLevel.Lesser,
                            Value = 0
                        },
                        new SurvivalStatKeyPoint{
                            Level = SurvivalStatHazardLevel.Strong,
                            Value = -25
                        },
                        new SurvivalStatKeyPoint{
                            Level = SurvivalStatHazardLevel.Max,
                            Value = -50
                        }
                    }
            };
            return stat;
        }

        private void DoStatCrossKeyPoint(SurvivalStat stat, SurvivalStatKeyPoint keyPoint)
        {
            var args = new SurvivalStatChangedEventArgs(stat, keyPoint);
            StatCrossKeyValue?.Invoke(this, args);
        }
    }
}
