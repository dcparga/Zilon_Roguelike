﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Zilon.Core.Common;
using Zilon.Core.CommonServices.Dices;
using Zilon.Core.Schemes;
using Zilon.Core.World;
using Zilon.Core.WorldGeneration.AgentCards;
using Zilon.Core.WorldGeneration.NameGeneration;

namespace Zilon.Core.WorldGeneration
{
    /// <summary>
    /// Экземпляр генератора мира с историей.
    /// </summary>
    /// <seealso cref="IWorldGenerator" />
    public class WorldGenerator : IWorldGenerator
    {
        private const int WORLD_SIZE = 40;
        private const int START_ITERATION_REALMS = 8;
        private const int HISTORY_ITERATION_COUNT = 40_000;
        private const int StartAgentCount = 40;
        private const int LocationBaseSize = 20;
        private const string CITY_SCHEME_SID = "city";
        private const string WILD_SCHEME_SID = "forest";

        private readonly IDice _dice;
        private readonly ISchemeService _schemeService;

        /// <summary>
        /// Создаёт экземпляр <see cref="WorldGenerator"/>.
        /// </summary>
        /// <param name="dice"> Игровая кость, которая будет использована для рандомицзации событий мира.
        /// В ближайшее время будет заменена специализированным источником рандома.
        /// </param>
        /// <param name="schemeService"> Сервис для доступа к схемам. Используется для генерации карты локации в провинции. </param>
        public WorldGenerator(IDice dice, ISchemeService schemeService)
        {
            _dice = dice;
            _schemeService = schemeService;
        }

        /// <summary>
        /// Создание игрового мира с историей и граф провинций.
        /// </summary>
        /// <returns>
        /// Возвращает объект игрового мира.
        /// </returns>
        public Task<GlobeGenerationResult> GenerateGlobeAsync()
        {
            var globe = new Globe
            {
                Terrain = new TerrainCell[WORLD_SIZE][],
                agentNameGenerator = new RandomName(_dice),
                cityNameGenerator = new CityNameGenerator(_dice)
            };

            var realmTask = CreateRealms(globe);
            var terrainTask = CreateTerrain(globe);

            Task.WaitAll(realmTask, terrainTask);

            CreateStartLocalities(globe);
            CreateStartAgents(globe);

            var agentsClock = new Stopwatch();
            agentsClock.Start();

            var cardQueue = CreateAgentCardQueue();

            // обработка итераций
            ProcessIterations(globe, cardQueue);


            globe.StartProvince = GetStartProvinceCoords(globe);
            globe.HomeProvince = GetHomeProvinceCoords(globe, globe.StartProvince);

            agentsClock.Stop();
            Console.WriteLine(agentsClock.ElapsedMilliseconds / 1f + "ms");

            // Сейчас история пустая. Пока не разработаны требования, как лучше сделать.
            var globeHistory = new GlobeGenerationHistory();
            var result = new GlobeGenerationResult(globe, globeHistory);
            return Task.FromResult(result);
        }

        private TerrainCell GetStartProvinceCoords(Globe globe)
        {
            // Выбираем основной город, являющийся стартовым.
            // По городу определяем провинцию.

            var availableStartLocalities = globe.Localities;

            Locality startLocality;
            if (availableStartLocalities.Any())
            {
                var startLocalityIndex = _dice.Roll(0, availableStartLocalities.Count() - 1);
                startLocality = availableStartLocalities[startLocalityIndex];
            }
            else
            {
                startLocality = globe.Localities.FirstOrDefault();
            }

            if (startLocality == null)
            {
                //TODO Создать отдельный класс исключений GlobeGenerationException.
                throw new InvalidOperationException("Не удалось выбрать стартовую локацию.");
            }

            return startLocality.Cell;
        }

        //TODO Постараться объединить GetStartProvinceCoords и GetHomeProvinceCoords
        private TerrainCell GetHomeProvinceCoords(Globe globe, TerrainCell startProvince)
        {
            // Выбираем основной город, являющийся стартовым.
            // По городу определяем провинцию.

            var currentCubeCoords = HexHelper.ConvertToCube(startProvince.Coords);
            var availableLocalities = globe.Localities
                .Where(x => x.Cell != startProvince)
                .Where(x => HexHelper.ConvertToCube(x.Cell.Coords).DistanceTo(currentCubeCoords) > 2)
                .Where(x => HexHelper.ConvertToCube(x.Cell.Coords).DistanceTo(currentCubeCoords) <= 5)
                .ToArray();

            Locality selectedLocality;
            if (availableLocalities.Any())
            {
                var localityIndex = _dice.Roll(0, availableLocalities.Count() - 1);
                selectedLocality = availableLocalities[localityIndex];
            }
            else
            {
                selectedLocality = globe.Localities.LastOrDefault();
            }

            if (selectedLocality == null)
            {
                //TODO Создать отдельный класс исключений GlobeGenerationException.
                throw new InvalidOperationException("Не удалось выбрать локацию для дома.");
            }

            return selectedLocality.Cell;
        }

        /// <summary>
        /// Создание
        /// </summary>
        /// <param name="globe">Объект игрового мира, для которого создаётся локация.</param>
        /// <param name="cell">Провинция игрового мира из указанного выше <see cref="Globe" />,
        /// для которого создаётся локация.</param>
        /// <returns>
        /// Возвращает граф локация для провинции.
        /// </returns>
        public Task<GlobeRegion> GenerateRegionAsync(Globe globe, TerrainCell cell)
        {
            var region = new GlobeRegion(LocationBaseSize);

            // Сейчас допускаем, что паттерны квадратные, меньше размера провинции.
            // Пока не вращаем и не искажаем.
            // Там, где может быть объект, гарантированно создаём один город и два подземелья.
            var regionDraft = new GlobeRegionDraftValue[LocationBaseSize, LocationBaseSize];
            var startPattern = GlobeRegionPatterns.Start;
            var homePattern = GlobeRegionPatterns.Home;
            // Расчитываем размер паттернов.
            // Исходим из того, что пока все паттерны квадратные и одинаковые по размеру.
            // Поэтому размер произвольного паттерна будет справедлив для всех остальных.
            // Паттерн старта выбран произвольно.
            var patternSize = startPattern.Values.GetUpperBound(0) - startPattern.Values.GetLowerBound(0) + 1;

            // Вставляем паттерны в указанные области
            ApplyRegionPattern(ref regionDraft, GetDefaultPattrn(), 1, 1);
            ApplyRegionPattern(ref regionDraft, GetDefaultPattrn(), LocationBaseSize - patternSize - 1, 1);
            ApplyRegionPattern(ref regionDraft, GetDefaultPattrn(), 1, LocationBaseSize - patternSize - 1);
            ApplyRegionPattern(ref regionDraft, GetDefaultPattrn(), LocationBaseSize - patternSize - 1, LocationBaseSize - patternSize - 1);
            if (globe.StartProvince == cell)
            {
                ApplyRegionPattern(ref regionDraft, startPattern, (LocationBaseSize - patternSize) / 2, (LocationBaseSize - patternSize) / 2);
            }
            else if (globe.HomeProvince == cell)
            {
                ApplyRegionPattern(ref regionDraft, homePattern, (LocationBaseSize - patternSize) / 2, (LocationBaseSize - patternSize) / 2);
            }
            else
            {
                ApplyRegionPattern(ref regionDraft, GetDefaultPattrn(), (LocationBaseSize - patternSize) / 2, (LocationBaseSize - patternSize) / 2);
            }

            for (var x = regionDraft.GetLowerBound(0); x <= regionDraft.GetUpperBound(0); x++)
            {
                for (var y = regionDraft.GetLowerBound(1); y <= regionDraft.GetUpperBound(1); y++)
                {
                    ValidateRegion(region, regionDraft, x, y);
                }
            }

            return Task.FromResult(region);
        }

        private void ValidateRegion(GlobeRegion region, GlobeRegionDraftValue[,] regionDraft, int x, int y)
        {
            // Определяем, является ли узел граничным. На граничных узлах ничего не создаём.
            // Потому что это может вызвать трудности при переходах между провинциями.
            // Например, игрок при переходе сразу может попасть в данж или город.
            // Не отлажен механиз перехода, если часть узлов соседней провинции отсутствует.
            var isBorder = x == 0 || x == LocationBaseSize - 1 || y == 0 || y == LocationBaseSize - 1;
            if (isBorder)
            {
                AddNodeIfBorder(region, x, y);
                return;
            }

            var currentPatternValue = regionDraft[x, y];
            GlobeRegionNode node = null;
            if (currentPatternValue == null || currentPatternValue.Value.HasFlag(GlobeRegionDraftValueType.Wild))
            {
                // Это означает, что сюда не был применен ни один шаблон или
                // Дикий сектор был указан явно одним из шаблонов.
                // Значит генерируем просто дикий сектор.
                var locationScheme = _schemeService.GetScheme<ILocationScheme>(WILD_SCHEME_SID);
                node = new GlobeRegionNode(x, y, locationScheme);
            }
            else if (currentPatternValue.IsStart)
            {
                var locationScheme = _schemeService.GetScheme<ILocationScheme>(WILD_SCHEME_SID);
                node = new GlobeRegionNode(x, y, locationScheme)
                {
                    IsStart = true
                };
            }
            else if (currentPatternValue.IsHome)
            {
                var locationScheme = _schemeService.GetScheme<ILocationScheme>(CITY_SCHEME_SID);
                node = new GlobeRegionNode(x, y, locationScheme)
                {
                    IsTown = true,
                    IsHome = true
                };
            }
            else if (currentPatternValue.Value.HasFlag(GlobeRegionDraftValueType.Town))
            {
                var locationScheme = _schemeService.GetScheme<ILocationScheme>(CITY_SCHEME_SID);
                node = new GlobeRegionNode(x, y, locationScheme)
                {
                    IsTown = true
                };
            }
            else if (currentPatternValue.Value.HasFlag(GlobeRegionDraftValueType.Dungeon))
            {
                var locationSchemeSids = new[] 
                {
                "rat-hole",
                "rat-kingdom",
                "demon-dungeon",
                "demon-lair",
                "crypt",
                "elder-place",
                "genomass-cave"
                };
                var locationSidIndex = _dice.Roll(0, locationSchemeSids.Length - 1);
                var locationSid = locationSchemeSids[locationSidIndex];
                var locationScheme = _schemeService.GetScheme<ILocationScheme>(locationSid);
                node = new GlobeRegionNode(x, y, locationScheme);
            }
            else
            {
                Debug.Assert(true, "При генерации провинции должны все исходы быть предусмотрены.");
            }

            if (node != null)
            {
                region.AddNode(node);
            }
        }

        private void AddNodeIfBorder(GlobeRegion region, int x, int y)
        {
            var locationScheme = _schemeService.GetScheme<ILocationScheme>(WILD_SCHEME_SID);
            var borderNode = new GlobeRegionNode(x, y, locationScheme)
            {
                IsBorder = true
            };
            region.AddNode(borderNode);
        }

        private GlobeRegionPattern GetDefaultPattrn()
        {
            var defaultPatterns = new[]
            {
                GlobeRegionPatterns.Angle,
                GlobeRegionPatterns.Tringle,
                GlobeRegionPatterns.Linear,
                GlobeRegionPatterns.Diagonal
            };

            var defaultPatternIndex = _dice.Roll(0, defaultPatterns.Length - 1);
            var defaultPattern = defaultPatterns[defaultPatternIndex];
            return defaultPattern;
        }

        /// <summary>
        /// Применяет шаблон участка провинции на черновик провинции в указанных координатах.
        /// </summary>
        /// <param name="regionDraft"> Черновик провинции.
        /// Отмечен ref, чтобы было видно, что метод именяет этот объект. </param>
        /// <param name="pattern"> Шаблон, применяемый на черновик. </param>
        /// <param name="insertX"> Х-координата применения шаблона. </param>
        /// <param name="insertY"> Y-координата применения шаблона. </param>
        private void ApplyRegionPattern(ref GlobeRegionDraftValue[,] regionDraft,
                                         GlobeRegionPattern pattern,
                                         int insertX,
                                         int insertY)
        {
            // Пока костыльное решение из расчёта, что во всех паттернах будет 3 объекта интереса.
            var townCount = CountTownPlaces(pattern.Values);
            var townIndex = _dice.Roll(0, townCount - 1);

            var rotateValueIndex = _dice.Roll(0, (int)MatrixRotation.ConterClockwise90);
            var rotatedPatternValues = MatrixHelper.Rotate(pattern.Values, (MatrixRotation)rotateValueIndex);
            ApplyRegionPatternInner(regionDraft, rotatedPatternValues, insertX, insertY, townIndex);
        }

        private static int CountTownPlaces(GlobeRegionPatternValue[,] patternValues)
        {
            var counter = 0;
            foreach (var value in patternValues)
            {
                if (value == null)
                {
                    continue;
                }

                if (value.HasObject)
                {
                    counter++;
                }
            }

            return counter;
        }

        private static void ApplyRegionPatternInner(GlobeRegionDraftValue[,] regionDraft,
                                                    GlobeRegionPatternValue[,] patternValues,
                                                    int insertX,
                                                    int insertY,
                                                    int townIndex)
        {
            var interestObjectCounter = 0;

            for (var x = patternValues.GetLowerBound(0); x <= patternValues.GetUpperBound(0); x++)
            {
                for (var y = patternValues.GetLowerBound(1); y <= patternValues.GetUpperBound(1); y++)
                {
                    GlobeRegionDraftValue draftValue = null;

                    // TODO Для диких секторов нужно ввести отдельное значение шаблона.
                    // Потому что в шаблонах планируются ещё пустые (непроходимые) узлы. Но и для них будет специальное значение.
                    // А пустота (null) будет означать, что ничего делать не нужно (transparent).
                    // Потому что будут ещё шаблоны, накладываемые поверх всех в случайные места, чтобы генерировать дополнительные 
                    // мусорные объекты.
                    var patternValue = patternValues[x, y];
                    if (patternValue == null)
                    {
                        draftValue = new GlobeRegionDraftValue(GlobeRegionDraftValueType.Wild);
                    }
                    else if (patternValue.IsStart)
                    {
                        draftValue = new GlobeRegionDraftValue(GlobeRegionDraftValueType.Dungeon)
                        {
                            IsStart = true
                        };
                    }
                    else if (patternValue.IsHome)
                    {
                        draftValue = new GlobeRegionDraftValue(GlobeRegionDraftValueType.Town)
                        {
                            IsHome = true
                        };
                    }
                    else if (patternValue.HasObject)
                    {
                        if (interestObjectCounter == townIndex)
                        {
                            draftValue = new GlobeRegionDraftValue(GlobeRegionDraftValueType.Town);
                        }
                        else
                        {
                            draftValue = new GlobeRegionDraftValue(GlobeRegionDraftValueType.Dungeon);
                        }
                        interestObjectCounter++;
                    }

                    regionDraft[x + insertX, y + insertY] = draftValue;
                }
            }
        }

        private void ProcessIterations(Globe globe, Queue<IAgentCard> cardQueue)
        {
            for (var iteration = 0; iteration < HISTORY_ITERATION_COUNT; iteration++)
            {

                foreach (var agent in globe.Agents.ToArray())
                {
                    var useCardRoll = _dice.Roll2D6();
                    if (useCardRoll > 7)
                    {
                        continue;
                    }

                    var card = cardQueue.Dequeue();

                    if (card.CanUse(agent, globe))
                    {
                        card.Use(agent, globe, _dice);
                    }

                    cardQueue.Enqueue(card);
                }
            }
        }

        private static Queue<IAgentCard> CreateAgentCardQueue()
        {
            return new Queue<IAgentCard>(new IAgentCard[] {
                new ChangeLocality(),
                new CreateLocality(),
                new IncreasePopulation(),
                new AgentOpposition(),
                new AgentSupport(),
                new Disciple(),
                new TakeLocation()
            });
        }

        private void CreateStartAgents(Globe globe)
        {
            for (var i = 0; i < StartAgentCount; i++)
            {
                var rolledLocalityIndex = _dice.Roll(0, globe.Localities.Count - 1);
                var locality = globe.Localities[rolledLocalityIndex];

                var agentName = globe.agentNameGenerator.Generate(Sex.Male, 1);

                var agent = new Agent
                {
                    Name = agentName,
                    Location = locality.Cell,
                    Realm = locality.Owner
                };

                globe.Agents.Add(agent);

                CacheHelper.AddAgentToCell(globe.AgentCells, locality.Cell, agent);

                var rolledBranchIndex = _dice.Roll(0, 7);
                agent.Skills = new Dictionary<BranchType, int>
                {
                    { (BranchType)rolledBranchIndex, 1 }
                };
            }
        }

        private void CreateStartLocalities(Globe globe)
        {
            for (var i = 0; i < START_ITERATION_REALMS; i++)
            {
                var randomX = _dice.Roll(0, WORLD_SIZE - 1);
                var randomY = _dice.Roll(0, WORLD_SIZE - 1);

                var localityName = globe.GetLocalityName(_dice);

                var locality = new Locality()
                {
                    Name = localityName,
                    Cell = globe.Terrain[randomX][randomY],
                    Owner = globe.Realms[i],
                    Population = 3
                };

                var rolledBranchIndex = _dice.Roll(0, 7);
                locality.Branches = new Dictionary<BranchType, int>
                        {
                            { (BranchType)rolledBranchIndex, 1 }
                        };

                globe.Localities.Add(locality);

                globe.LocalitiesCells[locality.Cell] = locality;

                globe.ScanResult.Free.Remove(locality.Cell);
            }
        }

        private Task CreateTerrain(Globe globe)
        {
            for (var i = 0; i < WORLD_SIZE; i++)
            {
                globe.Terrain[i] = new TerrainCell[WORLD_SIZE];

                for (var j = 0; j < WORLD_SIZE; j++)
                {
                    globe.Terrain[i][j] = new TerrainCell
                    {
                        Coords = new OffsetCoords(i, j)
                    };

                    globe.ScanResult.Free.Add(globe.Terrain[i][j]);
                }
            }

            return Task.CompletedTask;
        }

        private Task CreateRealms(Globe globe)
        {
            var realmColors = new[] { Color.Red, Color.Green, Color.Blue, Color.Yellow,
            Color.Beige, Color.LightGray, Color.Magenta, Color.Cyan};
            for (var i = 0; i < START_ITERATION_REALMS; i++)
            {
                var realm = new Realm
                {
                    Name = _realmNames[i],
                    Banner = new RealmBanner { MainColor = realmColors[i] }
                };

                globe.Realms.Add(realm);
            }

            return Task.CompletedTask;
        }

        private readonly string[] _realmNames = new[] {
            "Sons Of The Law",
            "Gaarn Folk",
            "Sun'Ost Union",
            "Hellgrimar Republik",
            "Anklav Of The Seven Seas",
            "Eagle Home Keepers",
            "Cult of Liquid DOG",
            "Free Сities Сouncil"
        };
    }
}
