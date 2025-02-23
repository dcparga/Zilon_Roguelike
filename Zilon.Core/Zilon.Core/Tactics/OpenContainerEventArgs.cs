﻿using System;
using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

namespace Zilon.Core.Tactics
{
    /// <summary>
    /// Аргументы события на открытие контейнера.
    /// </summary>
    public sealed class OpenContainerEventArgs : EventArgs
    {
        /// <summary>
        /// Контейнер, который пытались открыть.
        /// </summary>
        [PublicAPI]
        public IPropContainer Container { get; }

        /// <summary>
        /// Результат открытия. <see cref="SuccessOpenContainerResult">SuccessOpenContainerResult</see>, открытие прошло успешно.
        /// </summary>
        [PublicAPI]
        public IOpenContainerResult Result { get; }

        [ExcludeFromCodeCoverage]
        public OpenContainerEventArgs(IPropContainer container, [NotNull] IOpenContainerResult result)
        {
            Container = container;
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }
    }
}
