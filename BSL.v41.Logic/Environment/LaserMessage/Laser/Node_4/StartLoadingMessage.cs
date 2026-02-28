using BSL.v41.Logic.Environment.LaserMessage.Sepo.Player;
using BSL.v41.Titan.DataStream.Helps;

namespace BSL.v41.Logic.Environment.LaserMessage.Laser.Node_4;

public class StartLoadingMessage : PiranhaMessage
{
    private int _gameModeVariation;
    private bool _isFriendlyMatch;
    private bool _isSpectate;
    private bool _isUnderdog;
    private static readonly Random _random = new();
    private int _location;               // полный GlobalId локации (например, 15000000 + id)
    private LogicPlayer _logicPlayer = null!;
    private List<LogicPlayer> _logicPlayerMap = null!;

    public override void Encode()
    {
        base.Encode();

        // Основная информация о матче
        ByteStream.WriteInt(_logicPlayerMap.Count);          // общее количество игроков
        ByteStream.WriteInt(_logicPlayer.GetPlayerIndex());  // индекс текущего игрока
        ByteStream.WriteInt(_logicPlayer.GetTeamIndex());    // команда текущего игрока
        ByteStream.WriteInt(_logicPlayerMap.Count);          // дублирование (как в оригинале)

        // Сериализация всех игроков
        foreach (var player in _logicPlayerMap)
            player.Encode(ByteStream);

        // Параметры битвы
        ByteStream.WriteInt(0);                               // длительность боя (0 = бесконечно)
        ByteStream.WriteInt(_random.Next());           // сид рандома (уникальный для каждой битвы)
        ByteStream.WriteInt(0);                               // скин карты (можно взять из LocationData)

        // Режим игры
        ByteStream.WriteVInt(_gameModeVariation);

        // Определяем количество команд и игроков в команде
        bool hasTwoTeams = IsTeamMode(_gameModeVariation);
        int playersPerTeam = GetPlayersPerTeam(_gameModeVariation);

        ByteStream.WriteVInt(hasTwoTeams ? 2 : 1);           // количество команд
        ByteStream.WriteVInt(playersPerTeam);                 // игроков в команде

        ByteStream.WriteBoolean(true);                         // неизвестный флаг (всегда true)

        // Исправлено: WriteBoolean вместо WriteVInt для _isSpectate
        ByteStream.WriteBoolean(_isSpectate);
        ByteStream.WriteVInt(0);                                // неизвестно

        // Локация (карта)
        ByteStreamHelper.WriteDataReference(ByteStream, _location);

        ByteStream.WriteBoolean(false);                         // player map (вероятно, не нужно)
        ByteStream.WriteBoolean(_isUnderdog);
        ByteStream.WriteBoolean(_isFriendlyMatch);
        ByteStream.WriteVInt(0);
        ByteStream.WriteVInt(0);

        ByteStream.WriteVInt(0);
        ByteStream.WriteVInt(0);

        // Отладочный вывод
        Console.WriteLine($"[StartLoading] Players: {_logicPlayerMap.Count}, my index {_logicPlayer.GetPlayerIndex()}, " +
                          $"team {_logicPlayer.GetTeamIndex()}, mode {_gameModeVariation}, " +
                          $"teams {(hasTwoTeams ? 2 : 1)}, perTeam {playersPerTeam}, location {_location}, " +
                          $"spectate {_isSpectate}");
    }

    // Вспомогательные методы для определения параметров режима
    private bool IsTeamMode(int gameModeVariation)
    {
        // Замените на реальные значения вашего проекта
        // Пример: 1 = 3v3, 2 = дуо, 6 = соло (как в вашем MatchmakingManager)
        return gameModeVariation == 1 || gameModeVariation == 2;
    }

    private int GetPlayersPerTeam(int gameModeVariation)
    {
        // Замените на реальную логику
        if (gameModeVariation == 1)      // 3v3
            return 3;
        if (gameModeVariation == 2)      // дуо
            return 2;
        // Соло и другие режимы
        return 1;
    }

    public override void Destruct() => base.Destruct();

    // Геттеры и сеттеры
    public int GetLocation() => _location;
    public void SetLocation(int location) => _location = location;
    public int GetGameModeVariation() => _gameModeVariation;
    public void SetGameModeVariation(int gameModeVariation) => _gameModeVariation = gameModeVariation;
    public List<LogicPlayer> GetLogicPlayerMap() => _logicPlayerMap;
    public void SetLogicPlayerMap(List<LogicPlayer> logicPlayerMap) => _logicPlayerMap = logicPlayerMap;
    public LogicPlayer GetLogicPlayer() => _logicPlayer;
    public void SetLogicPlayer(LogicPlayer logicPlayer) => _logicPlayer = logicPlayer;
    public bool GetIsSpectate() => _isSpectate;
    public void SetIsSpectate(bool spectate) => _isSpectate = spectate;
    public bool GetIsUnderdog() => _isUnderdog;
    public void SetIsUnderdog(bool value) => _isUnderdog = value;
    public bool GetIsFriendlyMatch() => _isFriendlyMatch;
    public void SetIsFriendlyMatch(bool value) => _isFriendlyMatch = value;

    public override int GetMessageType() => 20559;
    public override int GetServiceNodeType() => 4;
}