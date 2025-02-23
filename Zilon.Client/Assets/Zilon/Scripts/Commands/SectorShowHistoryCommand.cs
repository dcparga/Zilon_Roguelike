﻿using Zenject;

using Zilon.Core.Client.Windows;
using Zilon.Core.Commands;

namespace Assets.Zilon.Scripts.Commands
{
    /// <summary>
    /// Команда для отображения истории из сектора.
    /// </summary>
    /// <remarks>
    /// Сделаны отдельно команды для сектора и для глобальной карты,
    /// потому что у них рависимости от похожих, но разных сервисов.
    /// 
    /// Эта команда используется, когда читаем книгу истории из инвернтаря.
    /// </remarks>
    internal sealed class SectorShowHistoryCommand : ICommand
    {
        [Inject]
        private ISectorModalManager _sectorModalManager;

        public bool CanExecute()
        {
            return true;
        }

        public void Execute()
        {
            _sectorModalManager.ShowHistoryBookModal();
        }
    }
}
