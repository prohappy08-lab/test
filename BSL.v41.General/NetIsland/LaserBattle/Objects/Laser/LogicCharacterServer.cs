using System.Reflection;
using BSL.v41.General.NetIsland.LaserBattle;
using BSL.v41.General.NetIsland.LaserBattle.Objects;
using BSL.v41.General.NetIsland.LaserBattle.Significant;
using BSL.v41.General.NetIsland.LaserBattle.Significant.Accessory;
using BSL.v41.General.NetIsland.LaserBattle.Significant.Fly;
using BSL.v41.Logic.Environment.LaserMessage.Laser.Node_1;
using BSL.v41.Supercell.Titan.CommonUtils.Utils;
using BSL.v41.Titan.DataStream;
using BSL.v41.Titan.Mathematical;
using BSL.v41.Titan.Mathematical.Data;
using BSL.v41.Tools.LaserCsv;
using BSL.v41.Tools.LaserCsv.Manufacturer.Laser;

namespace BSL.v41.General.NetIsland.LaserBattle.Objects.Laser
{
    public class LogicCharacterServer : LogicGameObjectServer
    {
        public const int DamageNumbersDelayTicks = 2;

        private readonly int _classId;
        private readonly int _instanceId;
        private readonly LogicBattleModeServer _logicBattleModeServer1;
        private int _afkTicks;
        private int _attackAngle;
        private int _attackingTicker;
        private int _characterState;
        private FlyParticle _flyParticle = null!;
        private int _gameModeVariation;
        private int _interruptSkillsTick;
        private bool _isMoving;
        private bool _isPlayerControlRemoved;
        private LogicCharacterData? _logicCharacterData;
        private LogicSkillData _logicSkillData1 = null!;
        private LogicSkillData _logicSkillData2 = null!;
        private List<LogicSkillServer> _logicSkillServers = new();
        private int _maxHitPoints;
        private int _moveAngle;
        private int _movementCount;
        private int _moveTicker;
        private int _localSpeedFactor;
        private int _nowHitPoints;
        private LogicVector2 _nowPosition = new();
        private int _oldDa;
        private int _oldDx;
        private int _oldDy;
        private LogicVector2 _requiredPosition = new();
        private LogicVector2 _requiredPositionDelta = new();
        private LogicVector2 _startPosition = new();
        private bool _ultimateVisualEffect;
        public LogicAccessory? LogicAccessory;

        public LogicCharacterServer(LogicBattleModeServer logicBattleModeServer, int classId, int instanceId, int index)
            : base(logicBattleModeServer, classId, instanceId, 0, index)
        {
            _classId = classId;
            _instanceId = instanceId;
            _logicBattleModeServer1 = logicBattleModeServer;
        }

        public void InitializeMembers(bool playerSector = false)
        {
            Console.WriteLine($"[DEBUG] InitializeMembers called for classId={_classId}, instanceId={_instanceId}, playerSector={playerSector}");
            _logicSkillServers = new List<LogicSkillServer>();

            if (!playerSector)
            {
                _gameModeVariation = LogicDataTables.GetGameModeVariationByName(
                    ((LogicLocationData)LogicDataTables.GetDataById(_logicBattleModeServer1.GetLocation() < 1000000
                        ? GlobalId.CreateGlobalId(CsvHelperTable.Locations.GetId(), _logicBattleModeServer1.GetLocation())
                        : _logicBattleModeServer1.GetLocation())).GetGameModeVariation()).GetVariation();

                int globalId = GlobalId.CreateGlobalId(_classId, _instanceId);
                Console.WriteLine($"[DEBUG] Loading LogicCharacterData with globalId={globalId}");
                _logicCharacterData = (LogicCharacterData)LogicDataTables.GetDataById(globalId);
                if (_logicCharacterData == null)
                {
                    Console.WriteLine($"[ERROR] Failed to load LogicCharacterData for globalId={globalId}");
                    return;
                }

                if (_logicCharacterData.GetWeaponSkill() != "")
                    _logicSkillData1 = LogicDataTables.GetSkillByName(_logicCharacterData.GetWeaponSkill());
                if (_logicCharacterData.GetUltimateSkill() != "")
                    _logicSkillData2 = LogicDataTables.GetSkillByName(_logicCharacterData.GetUltimateSkill());

                if (_logicCharacterData.GetWeaponSkill() != "" && _logicSkillData1 != null)
                {
                    _logicSkillServers.Add(new LogicSkillServer(GetLogicBattleModeServer(),
                        _logicSkillData1.GetClassId(), _logicSkillData1.GetInstanceId()));
                }

                if (_logicCharacterData.GetUltimateSkill() != "" && _logicSkillData2 != null)
                {
                    _logicSkillServers.Add(new LogicSkillServer(GetLogicBattleModeServer(),
                        _logicSkillData2.GetClassId(), _logicSkillData2.GetInstanceId()));
                }

                _maxHitPoints = _logicCharacterData.GetHitpoints();
                _nowHitPoints = _maxHitPoints;
                _interruptSkillsTick = 0;
                _movementCount = 1;
                _moveTicker = -1;
                _attackingTicker = 63;
                _attackAngle = 0;
                _afkTicks = 0;
                _characterState = 0;
                _oldDa = 0;
                _oldDx = 0;
                _oldDy = 0;
                _localSpeedFactor = 1;
                _isMoving = false;
                _isPlayerControlRemoved = false;
                _ultimateVisualEffect = false;
            }

            if (playerSector)
            {
                Console.WriteLine($"[DEBUG] InitializeMembers playerSector TRUE for character {GetObjectGlobalId()}");
                if (_logicCharacterData == null)
                {
                    Console.WriteLine($"[ERROR] _logicCharacterData is null for character {GetObjectGlobalId()}");
                    return;
                }
                if (!_logicCharacterData.IsHero())
                {
                    Console.WriteLine($"[DEBUG] Character is not a hero, skipping player sector");
                    return;
                }

                var player = GetPlayer();
                if (player == null)
                {
                    Console.WriteLine($"[ERROR] GetPlayer() returned NULL for character {GetObjectGlobalId()}");
                    return;
                }
                Console.WriteLine($"[DEBUG] Player found, team={player.GetTeamIndex()}, cardInfo count={player.CardInfo.Count}");

                if (player.CardInfo.Count > 1)
                    LogicAccessory = new LogicAccessory(this,
                        LogicDataTables.GetAccessoryByName(player.CardInfo[1].GetSkill()));
                if (player.GetTeamIndex() == 0) _moveAngle = 90;
                else _moveAngle = 90 * 3;

                player.ChargeUlti(false, 10000, 100);
            }
        }

        public override void Tick()
        {
            TickSelfDestruct();
            TickTimers();
            HandleMoveAndAttack();

            if (_flyParticle != null!)
            {
                if (!_flyParticle.Update())
                    _flyParticle = null!;
                else
                    _afkTicks = -1;
            }

            LogicAccessory?.UpdateAccessory();
        }

        private void TickSelfDestruct()
        {
            var fields = typeof(LogicCharacterServer).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!field.Name.StartsWith('_') || !field.Name.EndsWith("Tick"))
                    continue;

                object? valueObj = field.GetValue(this);
                if (valueObj is int value)
                {
                    if (value < _logicBattleModeServer1.GetTicksGone())
                        field.SetValue(this, -1);
                }
            }
        }

        private void TickTimers()
        {
            if (_logicCharacterData == null || !_logicCharacterData.IsHero()) return;

            _afkTicks++;
            if (_logicBattleModeServer1.IsPlayerAfk(_afkTicks, this))
            {
                var disconnectedMessage = new DisconnectedMessage();
                disconnectedMessage.SetReason(2);
                // Отправка закомментирована
            }
        }

        public void SetStartPosition(LogicVector2 logicVector2)
        {
            _startPosition = logicVector2.Clone();
            SetPosition(_startPosition.GetX(), _startPosition.GetY(), GetZ());
            _nowPosition = _startPosition.Clone();
            _requiredPosition = _startPosition.Clone();
            _requiredPositionDelta = _startPosition.Clone();
        }

        public void MoveTo(int toX, int toY, int toXDelta, int toYDelta, bool flyMode = false)
        {
            Console.WriteLine($"[DEBUG MOVE] MoveTo called for {GetObjectGlobalId()}: toX={toX}, toY={toY}, flyMode={flyMode}, current pos=({GetX()},{GetY()})");
    if (!_logicBattleModeServer1.GetTileMap().LogicRect.IsInside(toX, toY)) 
    {
        Console.WriteLine($"[DEBUG MOVE] Target out of bounds");
        return;
    }
    if (_flyParticle != null!) 
    {
        Console.WriteLine($"[DEBUG MOVE] FlyParticle active, cannot move");
        return;
    }
            if (!_logicBattleModeServer1.GetTileMap().LogicRect.IsInside(toX, toY)) return;
            if (_flyParticle != null!) return;

            _requiredPosition = new LogicVector2(toX, toY);

            if (flyMode)
            {
                _isMoving = false;
                _characterState = 0;
                _moveTicker = 0;

                var deltaTile = _logicBattleModeServer1.GetTileMap().LogicTileMap.GetTile(toX, toY);

                if (deltaTile.TileData.GetTileCode() == 'W')
                {
                    var deltaTileDictionary = new Dictionary<int, LogicVector2>();

                    for (int pseudoX = 0; pseudoX < _logicBattleModeServer1.GetTileMap().RenderSystem.GetTilemapWidth(); pseudoX++)
                        for (int pseudoY = 0; pseudoY < _logicBattleModeServer1.GetTileMap().RenderSystem.GetTilemapHeight(); pseudoY++)
                        {
                            var tile = _logicBattleModeServer1.GetTileMap().LogicTileMap.GetTile(pseudoX, pseudoY, true);
                            if (tile.TileData.GetTileCode() != '.') continue;

                            var v1 = new LogicVector2(tile.LogicX, tile.LogicY);
                            var v2 = new LogicVector2(toX, toY);

                            deltaTileDictionary.TryAdd(v1.GetDistance(v2), v1);
                        }

                    var targetVector = deltaTileDictionary[deltaTileDictionary.Keys.Min()];
                    _requiredPosition.Set(targetVector.GetX(), targetVector.GetY());
                }

                var v2A = _requiredPosition.Clone();
                v2A.Substract(GetPosition().Clone());

                int deltaXproto = _requiredPosition.Clone().GetX() - GetPosition().Clone().GetX() > 0
                    ? LogicMath.Min(2700, _requiredPosition.Clone().GetX() - GetPosition().Clone().GetX())
                    : LogicMath.Max(-2700, _requiredPosition.Clone().GetX() - GetPosition().Clone().GetX());

                int deltaYproto = _requiredPosition.Clone().GetY() - GetPosition().Clone().GetY() > 0
                    ? LogicMath.Min(2700, _requiredPosition.Clone().GetY() - GetPosition().Clone().GetY())
                    : LogicMath.Max(-2700, _requiredPosition.Clone().GetY() - GetPosition().Clone().GetY());

                _moveAngle = LogicMath.GetAngle(deltaXproto, deltaYproto);
                _flyParticle = new FlyParticle(_requiredPosition);
                _flyParticle.SetParent(this, 3000);
                return;
            }

            _moveTicker = GetLogicBattleModeServer().GetTicksGone() + 100 + _logicBattleModeServer1.GetTick() / 20;
            _oldDa = _moveAngle;

            if (_isMoving) return;

            _movementCount++;
            _isMoving = true;

            _nowPosition = _requiredPosition.Clone();
            _requiredPosition = new LogicVector2(toX, toY);
            _requiredPositionDelta = new LogicVector2(toXDelta, toYDelta);
        }

        public void ActivateSkill(int x, int y, int skillType)
        {
            var v2A = new LogicVector2(x, y);
            _attackingTicker = skillType;
            _characterState = 2;
            _attackAngle = v2A.GetAngle();
            _moveAngle = _attackAngle - 1;
        }

       public void HandleMoveAndAttack()
{
    Console.WriteLine($"[DEBUG MOVE] HandleMoveAndAttack for {GetObjectGlobalId()}: movingSpeed={GetMovementSpeed()}, isMoving={_isMoving}, required=({_requiredPosition.GetX()},{_requiredPosition.GetY()})");

    var movingSpeed = GetMovementSpeed() * 1;

    if ((GetLogicBattleModeServer().GetTicksGone() < _moveTicker || _moveTicker < 0) &&
        GetPosition().Clone().GetDistance(_requiredPosition.Clone()) != 0 && movingSpeed > 0 && _isMoving)
    {
        if (_requiredPosition.Clone().GetX() - GetPosition().Clone().GetX() != 0 ||
            _requiredPosition.Clone().GetY() - GetPosition().Clone().GetY() != 0)
        {
            var v2A = _requiredPosition.Clone();
            v2A.Substract(GetPosition().Clone());

            int deltaXproto = _requiredPosition.Clone().GetX() - GetPosition().Clone().GetX() > 0
                ? LogicMath.Min(movingSpeed, _requiredPosition.Clone().GetX() - GetPosition().Clone().GetX())
                : LogicMath.Max(-movingSpeed, _requiredPosition.Clone().GetX() - GetPosition().Clone().GetX());

            int deltaYproto = _requiredPosition.Clone().GetY() - GetPosition().Clone().GetY() > 0
                ? LogicMath.Min(movingSpeed, _requiredPosition.Clone().GetY() - GetPosition().Clone().GetY())
                : LogicMath.Max(-movingSpeed, _requiredPosition.Clone().GetY() - GetPosition().Clone().GetY());

            int deltaX = (int)(LogicMath.Cos(LogicMath.GetAngle(v2A.GetX(), v2A.GetY())) /
                               (_logicBattleModeServer1.GetTick() * 1000 + 2000 - movingSpeed * _localSpeedFactor) *
                               _logicCharacterData!.GetSpeed() * _localSpeedFactor);

            int deltaY = (int)(LogicMath.Sin(LogicMath.GetAngle(v2A.GetX(), v2A.GetY())) /
                               (_logicBattleModeServer1.GetTick() * 1000 + 2000 - movingSpeed * _localSpeedFactor) *
                               _logicCharacterData.GetSpeed() * _localSpeedFactor);

            var deltaTile = _logicBattleModeServer1.GetTileMap().LogicTileMap
                .GetTile(deltaXproto + GetX(), deltaYproto + GetY());

            if ((!deltaTile.IsDestroyed() && deltaTile.TileData.GetBlocksMovement()) ||
                deltaTile.TileData.GetTileCode() == 'W')
            {
                var deltaTileDictionary = new Dictionary<int, LogicVector2>();

                for (int pseudoX = 0; pseudoX < _logicBattleModeServer1.GetTileMap().RenderSystem.GetTilemapWidth(); pseudoX++)
                    for (int pseudoY = 0; pseudoY < _logicBattleModeServer1.GetTileMap().RenderSystem.GetTilemapHeight(); pseudoY++)
                    {
                        var tile = _logicBattleModeServer1.GetTileMap().LogicTileMap.GetTile(pseudoX, pseudoY, true);
                        if (tile.TileData.GetTileCode() != '.') continue;

                        var v1 = new LogicVector2(tile.LogicX, tile.LogicY);
                        var v2 = new LogicVector2(deltaXproto + GetX(), deltaYproto + GetY());

                        deltaTileDictionary.TryAdd(v1.GetDistance(v2), v1);
                    }

                var targetVector = deltaTileDictionary[deltaTileDictionary.Keys.Min()];
                GetPosition().Set(targetVector.GetX(), targetVector.GetY());
                _nowPosition = GetPosition().Clone();
                _isMoving = false;
                Console.WriteLine($"[DEBUG MOVE] Position updated (obstacle) to ({GetX()},{GetY()})");
                return;
            }

            var v1d = GetPosition().Clone();
            v1d.Add(GetPosition().GetDistance(_requiredPosition.Clone()) > 156
                ? new LogicVector2(deltaX, deltaY)
                : new LogicVector2(deltaXproto, deltaYproto));

            GetPosition().Set(v1d.GetX() + 1, v1d.GetY());

            if (LogicMath.Abs(_moveAngle - LogicMath.NormalizeAngle360(LogicMath.GetAngle(deltaX, deltaY))) > 1 &&
                LogicMath.Abs(_oldDa - LogicMath.NormalizeAngle360(LogicMath.GetAngle(deltaX, deltaY))) > 1 &&
                (LogicMath.Cos(deltaXproto) + LogicMath.Sin(deltaYproto)) / 360 <= 360)
            {
                _moveAngle = LogicMath.NormalizeAngle360(LogicMath.GetAngle(deltaX, deltaY));
                _attackAngle = _moveAngle;
                _oldDa = _attackAngle;
            }

            _oldDx = deltaX;
            _oldDy = deltaY;
            _isMoving = false;
        }

        if (GetPosition().GetDistance(_requiredPosition.Clone()) <= 2)
            GetPosition().Set(_requiredPosition);

        _isMoving = GetPosition().GetDistance(_requiredPosition.Clone()) != 0;
        _nowPosition = GetPosition().Clone();
        Console.WriteLine($"[DEBUG MOVE] Position updated to ({GetX()},{GetY()})");
    }

    if (_attackingTicker < 63) _attackingTicker += 18;
    _attackingTicker = LogicMath.Clamp(_attackingTicker, 0, 63);
    if (_attackingTicker >= 63) _characterState = _isMoving ? 1 : 0;

    Console.WriteLine($"[DEBUG MOVE] End HandleMoveAndAttack: pos=({GetX()},{GetY()}), isMoving={_isMoving}, required=({_requiredPosition.GetX()},{_requiredPosition.GetY()})");
}

        public bool UltiEnabled(bool isCheckMode = false)
        {
            if (isCheckMode) return _ultimateVisualEffect;
            return _ultimateVisualEffect = true;
        }

        public void UltiDisabled() => _ultimateVisualEffect = false;
        public void ResetAfkTicks() => _afkTicks = 0;
        public void InterruptAllSkills() => _interruptSkillsTick = _logicBattleModeServer1.GetTicksGone() + _logicBattleModeServer1.GetTick() / 2;
        public bool IsPlayerControlRemoved() => _isPlayerControlRemoved;

        public override void Encode(BitStream bitStream, bool isOwnObject, int visionIndex, int visionTeam)
        {
            base.Encode(bitStream, isOwnObject, visionIndex, visionTeam);

            if (_logicCharacterData == null)
            {
                Console.WriteLine($"[WARN] _logicCharacterData is null for character {GetObjectGlobalId()}, attempting to reload...");
                int globalId = GlobalId.CreateGlobalId(_classId, _instanceId);
                _logicCharacterData = (LogicCharacterData)LogicDataTables.GetDataById(globalId);
                if (_logicCharacterData == null)
                {
                    Console.WriteLine($"[ERROR] Failed to reload LogicCharacterData for globalId={globalId}");
                    return;
                }
                else
                {
                    Console.WriteLine($"[INFO] Reloaded LogicCharacterData for character {GetObjectGlobalId()}");
                }
            }

            bool v0 = isOwnObject && _logicCharacterData.IsHero();
            bool v1 = IsPlayerControlRemoved();
            int v2 = LogicMath.Clamp(-1, 0, 255);
            bool v3 = _logicCharacterData.IsBoss();
            bool v4 = (_logicCharacterData.HasVeryMuchHitPoints() && !_logicCharacterData.IsHero()) ||
                      _logicCharacterData.GetHitpoints() > 65535;
            bool v5 = false;
            bool v6 = (_logicSkillData1?.GetChargedShotCount() ?? 0) >= 1;
            bool v7 = _localSpeedFactor > 1;
            int v8 = LogicMath.Clamp(_localSpeedFactor * 100, 1, 1023);
            int v9 = LogicMath.Clamp(_attackingTicker, 0, 63);

            if (_logicCharacterData.GetSpeed() > 0 || _logicCharacterData.HasAutoAttack() || _logicCharacterData.IsTrainingDummy())
            {
                if (v0)
                {
                    bitStream.WriteBoolean(v1);
                    bitStream.WriteBoolean(false);
                    if (v1)
                    {
                        bitStream.WritePositiveIntMax511(_moveAngle);
                        bitStream.WritePositiveIntMax511(_attackAngle);
                    }
                }
                else
                {
                    bitStream.WritePositiveIntMax511(_moveAngle);
                    bitStream.WritePositiveIntMax511(_attackAngle);
                }

                bitStream.WritePositiveIntMax7(_characterState);

                if (_logicCharacterData.GetHitpoints() > 3)
                {
                    bitStream.WriteBoolean(false);
                    bitStream.WriteIntMax63(v9);
                    bitStream.WriteBoolean(false);
                    if (bitStream.WriteBoolean(false)) bitStream.WriteBoolean(false);
                    bitStream.WriteBoolean(false);
                    bitStream.WriteBoolean(false);
                }
            }
            else
            {
                bitStream.WritePositiveIntMax7(_characterState);
                if (_logicCharacterData.IsTrain())
                {
                    bitStream.WritePositiveIntMax511(_moveAngle);
                    bitStream.WritePositiveIntMax511(_attackAngle);
                }
                else if (_logicCharacterData.GetAreaEffect() != "")
                {
                    bitStream.WritePositiveIntMax511(_moveAngle);
                }
            }

            bitStream.WritePositiveVIntMax65535OftenZero(0);
            bitStream.WritePositiveVIntMax65535OftenZero(0);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveVIntMax255OftenZero(0);

            int a101 = bitStream.WritePositiveVIntMax255OftenZero(0);
            for (int i = 0; i < a101; i++)
            {
                int a101L = bitStream.WritePositiveIntMax7(0);
                bitStream.WriteBoolean(a101L is 0 or 4);
            }

            bitStream.WriteBoolean(false);
            bitStream.WritePositiveVIntMax255OftenZero(v2);

            if (v3)
            {
                bitStream.WritePositiveIntMax2097151(_nowHitPoints);
                bitStream.WritePositiveIntMax2097151(_maxHitPoints);
            }
            else if (v4)
            {
                bitStream.WritePositiveIntMax524287(_nowHitPoints);
                bitStream.WritePositiveIntMax524287(_maxHitPoints);
            }
            else if (v5)
            {
                bitStream.WritePositiveIntMax262143(_nowHitPoints);
                bitStream.WritePositiveIntMax262143(_maxHitPoints);
            }
            else
            {
                bitStream.WritePositiveVIntMax65535(_nowHitPoints);
                bitStream.WritePositiveVIntMax65535(_maxHitPoints);
            }

            if (_logicCharacterData.IsDecoy())
                bitStream.WriteBoolean(false);

            if (_logicCharacterData.IsHero() || _logicCharacterData.IsDecoy())
            {
                bitStream.WritePositiveVIntMax255OftenZero(0);
                if (bitStream.WriteBoolean(false))
                {
                    bitStream.WritePositiveIntMax2047(0);
                    bitStream.WritePositiveIntMax2047(0);
                }
                bitStream.WriteBoolean(false);
            }

            if (_logicCharacterData.IsHero())
            {
                bitStream.WritePositiveVIntMax255OftenZero(0);
                bitStream.WriteBoolean(false);
                bitStream.WriteBoolean(false);

                if (bitStream.WriteBoolean(true))
                {
                    bitStream.WriteBoolean(false);
                    bitStream.WriteBoolean(false);
                    bitStream.WriteBoolean(false);
                    bitStream.WriteBoolean(false);
                    bitStream.WriteBoolean(_ultimateVisualEffect);
                }

                bitStream.WriteBoolean(false);

                if (v0 && bitStream.WriteBoolean(false))
                {
                    bitStream.WriteIntMax65535(0);
                    bitStream.WriteIntMax65535(0);
                }

                if (v6) bitStream.WriteIntMax3(0);
                bitStream.WriteBoolean(false);
            }

            if (_logicCharacterData.GetName() == "Baseball") bitStream.WriteIntMax3(0);
            if (_logicCharacterData.GetName() == "FireDude") bitStream.WritePositiveIntMax31(0);
            if (_logicCharacterData.GetName() == "PowerLeveler") bitStream.WritePositiveIntMax3(0);
            if (_logicCharacterData.GetName() == "RopeDude") bitStream.WriteBoolean(false);

            if (v3) bitStream.WriteBoolean(false);

            bitStream.WriteBoolean(false);
            bitStream.WritePositiveIntMax3(0);
            bitStream.WriteBoolean(false);
            bitStream.WritePositiveIntMax511(0);

            if (v0)
            {
                if (bitStream.WriteBoolean(false)) bitStream.WriteIntMax1023(v8);
                bitStream.WriteBoolean(false);
            }

            bitStream.WritePositiveIntMax31(0);

            if (_logicSkillServers != null)
            {
                foreach (var skillServer in _logicSkillServers)
                {
                    skillServer?.Encode(bitStream, _interruptSkillsTick > 0);
                }
            }
        }

        public override int GetType() => ObjectTypeHelperTable.Character.GetObjectType();
        public override int GetRadius() => _logicCharacterData?.GetCollisionRadius() ?? 100;
        public int GetMovementSpeed()
        {
            if (_logicCharacterData == null) return 0;
            return _localSpeedFactor * (_logicCharacterData.GetSpeed() / _logicBattleModeServer1.GetTick() - _logicBattleModeServer1.GetTick());
        }
        public int GetSpeedBuff() => _localSpeedFactor;
        public LogicCharacterData GetCharacterData() => _logicCharacterData!;
    }
}