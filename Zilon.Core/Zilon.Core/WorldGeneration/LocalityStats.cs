﻿using System.Collections.Generic;

namespace Zilon.Core.WorldGeneration
{
    /// <summary>
    /// Объект для хранения и передачи характеристик города.
    /// </summary>
    public sealed class LocalityStats
    {
        public LocalityStats()
        {
            Resources = new Dictionary<LocalityResource, int> {
                { LocalityResource.Food, 0 },
                { LocalityResource.Energy, 0 },
                { LocalityResource.Goods, 0 },
                { LocalityResource.LivingPlaces, 0 },
                { LocalityResource.Manufacture, 0 }
            };
        }

        /// <summary>
        /// Текущие ресурсы города.
        /// </summary>
        public Dictionary<LocalityResource, int> Resources { get; }

        ///// <summary>
        ///// Максимальное население в городе. Места проживания.
        ///// Если фактическое население превышает максимальное население в городе,
        ///// то начинается перенаселение. Жители начинают мигрировать.
        ///// Вырастает вероятность возникновения антисанитарии, криминала и т.д.
        ///// </summary>
        //public int PopulationLimit { get; set; }

        ///// <summary>
        ///// Текущий запас еды в городе.
        ///// Каждую итерацию одна единица населения будет требовать одной единицы пищи.
        ///// </summary>
        //public int CurrentFood { get; set; }

        ///// <summary>
        ///// Максимальный запас еды, который можно хранить в городе.
        ///// </summary>
        //public int FoodLimit { get; set; }

        ///// <summary>
        ///// Текущий запас энергии в городе.
        ///// </summary>
        //public int CurrentEnergy { get; set; }

        ///// <summary>
        ///// Максимальный запас энергии, который город может хранить.
        ///// </summary>
        //public int EnergyLimit { get; set; }

        ///// <summary>
        ///// Обеспечение населения города товарами (не едой).
        ///// Товары народного потребления требуются каждой единице населения.
        ///// Если баланс товаров опускается ниже нуля, начинается дефицит.
        ///// При дефиците население начинает мигрировать в другой город.
        ///// В режиме приключений в городе не будут продавать определённые товары.
        ///// </summary>
        //public int CurrentGoods { get; set; }

        ///// <summary>
        ///// Запас товаров народного потребления, которые город может хранить.
        ///// </summary>
        //public int GoodsLimit { get; set; }
    }
}
