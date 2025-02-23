﻿using Zilon.Core.Spec.Contexts;

namespace Zilon.Core.Spec.Steps
{
    public abstract class GenericStepsBase<TContext> where TContext: FeatureContextBase, new()
    {
        protected readonly TContext Context;

        protected GenericStepsBase(TContext context)
        {
            Context = context;
        }
    }
}
