using Assets.Zilon.Scripts.Commands;
using Assets.Zilon.Scripts.Services;

using Zenject;

using Zilon.Core.Commands;
using Zilon.Core.CommonServices.Dices;
using Zilon.Core.Persons;
using Zilon.Core.Players;
using Zilon.Core.Props;
using Zilon.Core.Schemes;
using Zilon.Core.Tactics;
using Zilon.Core.Tactics.Behaviour.Bots;
using Zilon.Core.World;
using Zilon.Core.WorldGeneration;

public class GlobalInstaller : MonoInstaller<GlobalInstaller>
{
    public SchemeLocator SchemeLocator;

    public override void InstallBindings()
    {
        RegisterDices();

        Container.Bind<IDecisionSource>().To<DecisionSource>().AsSingle();
        Container.Bind<ISchemeService>().To<SchemeService>().AsSingle();
        Container.Bind<ISchemeServiceHandlerFactory>().To<SchemeServiceHandlerFactory>().AsSingle();
        Container.Bind<IPropFactory>().To<PropFactory>().AsSingle();
        Container.Bind<IHumanPersonFactory>().To<RandomHumanPersonFactory>().AsSingle();
        Container.Bind<IDropResolver>().To<DropResolver>().AsSingle();
        Container.Bind<IDropResolverRandomSource>().To<DropResolverRandomSource>().AsSingle();
        Container.Bind<ISurvivalRandomSource>().To<SurvivalRandomSource>().AsSingle();
        Container.Bind<IPerkResolver>().To<PerkResolver>().AsSingle();
        Container.Bind<IScoreManager>().To<ScoreManager>().AsSingle();
        Container.Bind<ProgressStorageService>().AsSingle();
        Container.Bind<ScoreStorage>().AsSingle();


        Container.Bind<HumanPlayer>().AsSingle();
        Container.Bind<IBotPlayer>().To<BotPlayer>().AsSingle();

        Container.Bind<IWorldManager>().To<WorldManager>().AsSingle();
        Container.Bind<IWorldGenerator>().To<WorldGenerator>().AsSingle();


        Container.Bind<ISchemeLocator>().FromInstance(SchemeLocator).AsSingle();

        Container.Bind<ICommandBlockerService>().To<CommandBlockerService>().AsSingle();

        Container.Bind<ICommand>().WithId("quit-command").To<QuitCommand>().AsSingle();
        Container.Bind<ICommand>().WithId("quit-title-command").To<QuitTitleCommand>().AsSingle();
    }

    private void RegisterDices()
    {
        Container.Bind<IDice>().WithId("linear").To<LinearDice>().AsSingle();
        Container.Bind<IDice>().WithId("exp").To<ExpDice>().AsSingle();
        Container.Bind<IDice>().WithId("gauss").To<GaussDice>().AsSingle();

        // Во всех случаях, когда не нужна кость с конкретным распределением,
        // используем обычную кость с равномерным распределением.
        Container.Bind<IDice>().FromMethod(context => context.Container.ResolveId<IDice>("linear"));
    }
}