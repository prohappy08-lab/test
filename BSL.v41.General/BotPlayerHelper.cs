using BSL.v41.General.NetIsland.LaserBattle;
using BSL.v41.General.NetIsland.LaserBattle.Manager;
using BSL.v41.General.NetIsland.LaserBattle.Objects.Laser;
using BSL.v41.Logic.Environment.LaserMessage.Sepo.Player;
using BSL.v41.Titan.Mathematical;
using BSL.v41.Tools.LaserCsv;
using BSL.v41.Tools.LaserCsv.Manufacturer.Laser;
using System;
using System.Collections.Generic;
using BSL.v41.Titan.Mathematical.Data;

namespace BSL.v41.General.Service
{
    public static class BotPlayerHelper
    {
        private static readonly Random Random = new();

        public static LogicPlayer CreateBotPlayer(int teamIndex, int playerIndex, out long botAccountId)
        {
            botAccountId = 10000000 + Random.Next(100000, 999999);
            var player = new LogicPlayer();
            player.SetBot(true);
            player.SetTeamIndex(teamIndex);
            player.SetPlayerIndex(playerIndex);
            player.SessionId = botAccountId;

            var brawlerData = GetRandomBrawlerData();
            int brawlerInstanceId = brawlerData.GetInstanceId();
            player.HeroInfo.Add(brawlerInstanceId);
            player.HeroInfo.Add(1); // уровень
            player.HeroInfo.Add(0); // трофеи
            player.HeroInfo.Add(0); // скин

            var displayData = new PlayerDisplayData();
            displayData.SetAvatarName($"Bot_{playerIndex}");
            displayData.SetPlayerThumbnail(15);
            displayData.SetNameColor(0);
            player.SetPlayerDisplayData(displayData);

            return player;
        }

        public static void SpawnBotCharacter(LogicBattleModeServer battleServer, LogicPlayer botPlayer, int characterIndex)
        {
            int brawlerInstanceId = botPlayer.HeroInfo[0];
            int classId = 16;
            var gameObjManager = battleServer.GetLogicGameObjectManager();

            Console.WriteLine($"[DEBUG] SpawnBotCharacter: creating bot with instanceId={brawlerInstanceId}");
            var botCharacter = new BotCharacterServer(battleServer, classId, brawlerInstanceId, characterIndex);

            // ВАЖНО: инициализация ДО добавления в менеджер
            Console.WriteLine($"[DEBUG] SpawnBotCharacter: calling InitializeMembers...");
            botCharacter.InitializeMembers(); // playerSector = false
            Console.WriteLine($"[DEBUG] SpawnBotCharacter: after InitializeMembers, character data is {(botCharacter.GetCharacterData() != null ? "loaded" : "null")}");

            var spawnPos = GetSpawnPosition(battleServer, botPlayer.GetTeamIndex(), characterIndex);
            botCharacter.SetStartPosition(spawnPos);

            gameObjManager.AddLogicGameObject(botCharacter);
            botPlayer.OwnObjectId = botCharacter.GetObjectGlobalId();
            Console.WriteLine($"[DEBUG] SpawnBotCharacter: bot added with ObjectGlobalId={botPlayer.OwnObjectId}");

            battleServer.AddPlayer(botPlayer, null);
        }

        private static LogicCharacterData GetRandomBrawlerData()
        {
            // Перебираем всех персонажей и выбираем первого попавшегося героя
            int classId = 16;
            int instanceId = 0;
            while (true)
            {
                int globalId = GlobalId.CreateGlobalId(classId, instanceId);
                var data = LogicDataTables.GetDataById(globalId);
                if (data == null)
                    break;
                if (data is LogicCharacterData cd && cd.IsHero())
                {
                    Console.WriteLine($"[DEBUG] Selected brawler: {cd.GetName()} (instanceId={instanceId})");
                    return cd;
                }
                instanceId++;
            }
            // Если герой не найден, используем любого персонажа (instanceId = 0)
            int fallbackGlobalId = GlobalId.CreateGlobalId(16, 0);
            var fallbackData = LogicDataTables.GetDataById(fallbackGlobalId) as LogicCharacterData;
            if (fallbackData == null)
                throw new Exception("No characters available in CSV!");
            Console.WriteLine($"[DEBUG] Using fallback brawler: {fallbackData.GetName()}");
            return fallbackData;
        }

        private static LogicVector2 GetSpawnPosition(LogicBattleModeServer battleServer, int teamIndex, int index)
        {
            int width = battleServer.GetTileMap().RenderSystem.GetTilemapWidth() * 300;
            int height = battleServer.GetTileMap().RenderSystem.GetTilemapHeight() * 300;
            return new LogicVector2(width / 2, height / 2);
        }
    }
}