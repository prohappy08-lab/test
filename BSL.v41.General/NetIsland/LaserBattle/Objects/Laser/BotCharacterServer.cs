using BSL.v41.General.NetIsland.LaserBattle;
using BSL.v41.General.NetIsland.LaserBattle.Objects.Laser;
using BSL.v41.Titan.Mathematical;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BSL.v41.General.NetIsland.LaserBattle.Objects.Laser
{
    public class BotCharacterServer : LogicCharacterServer
    {
        private enum BotState
        {
            Idle,
            MovingToTarget,
            Attacking
        }

        private BotState _currentState = BotState.Idle;
        private LogicCharacterServer? _currentTarget;
        private int _attackCooldownTicks;
        private int _changeTargetCooldown;
        private readonly Random _random = new();

        public BotCharacterServer(LogicBattleModeServer logicBattleModeServer, int classId, int instanceId, int index)
            : base(logicBattleModeServer, classId, instanceId, index)
        {
        }

        public override void Tick()
        {
            // Вызываем базовую логику (таймеры, самоуничтожение)
            base.Tick();

            if (!IsAlive() || GetLogicBattleModeServer().IsGameOver())
                return;

            // Уменьшаем кулдауны
            if (_attackCooldownTicks > 0) _attackCooldownTicks--;
            if (_changeTargetCooldown > 0) _changeTargetCooldown--;

            Console.WriteLine($"[DEBUG BOT] Tick: bot {GetObjectGlobalId()}, alive={IsAlive()}, cooldowns: attack={_attackCooldownTicks}, target={_changeTargetCooldown}");
            DecideWhatToDo();
        }

        private void DecideWhatToDo()
        {
            var enemies = GetAllAliveEnemyCharacters();
            Console.WriteLine($"[DEBUG BOT] DecideWhatToDo: bot {GetObjectGlobalId()} found {enemies.Count} enemies");

            if (enemies.Count == 0)
            {
                Console.WriteLine($"[DEBUG BOT] No enemies, staying idle");
                return;
            }

            // Выбор цели (ближайший враг)
            if (_currentTarget == null || !_currentTarget.IsAlive() || _changeTargetCooldown <= 0)
            {
                var previousTarget = _currentTarget?.GetObjectGlobalId();
                _currentTarget = FindClosestEnemy(enemies);
                _changeTargetCooldown = 30; // ~1 секунда
                Console.WriteLine($"[DEBUG BOT] New target selected: {_currentTarget?.GetObjectGlobalId()} (previous {previousTarget})");
            }

            if (_currentTarget == null)
            {
                Console.WriteLine($"[DEBUG BOT] No valid target, returning");
                return;
            }

            float distance = GetPosition().GetDistance(_currentTarget.GetPosition());
            int attackRange = GetAttackRange();

            Console.WriteLine($"[DEBUG BOT] Target distance: {distance}, attack range: {attackRange}, attack cooldown: {_attackCooldownTicks}");

            if (distance <= attackRange && _attackCooldownTicks <= 0)
            {
                Console.WriteLine($"[DEBUG BOT] Decided to ATTACK target {_currentTarget.GetObjectGlobalId()}");
                PerformAttack();
            }
            else
            {
                Console.WriteLine($"[DEBUG BOT] Decided to MOVE towards target {_currentTarget.GetObjectGlobalId()}");
                MoveTowardsTarget();
            }
        }

        private List<LogicCharacterServer> GetAllAliveEnemyCharacters()
        {
            var result = new List<LogicCharacterServer>();
            var gameObjManager = GetLogicGameObjectManager();
            var allObjects = gameObjManager.GetNumGameObjects();
            int myTeam = GetPlayer()?.GetTeamIndex() ?? -1;

            foreach (var obj in allObjects)
            {
                if (obj is LogicCharacterServer character && character != this && character.IsAlive())
                {
                    var player = character.GetPlayer();
                    if (player != null && player.GetTeamIndex() != myTeam)
                        result.Add(character);
                }
            }
            Console.WriteLine($"[DEBUG BOT] GetAllAliveEnemyCharacters: found {result.Count} enemies for bot {GetObjectGlobalId()}");
            return result;
        }

        private LogicCharacterServer? FindClosestEnemy(List<LogicCharacterServer> enemies)
        {
            LogicCharacterServer? closest = null;
            float minDist = float.MaxValue;
            foreach (var enemy in enemies)
            {
                float dist = GetPosition().GetDistance(enemy.GetPosition());
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = enemy;
                }
            }
            Console.WriteLine($"[DEBUG BOT] FindClosestEnemy: closest enemy {closest?.GetObjectGlobalId()} at distance {minDist}");
            return closest;
        }

        private int GetAttackRange()
        {
            // Временная константа, позже можно брать из данных скилла
            return 1500;
        }

        private void PerformAttack()
        {
            if (_currentTarget == null) return;

            Console.WriteLine($"[DEBUG BOT] PerformAttack: bot {GetObjectGlobalId()} ATTACKING target {_currentTarget.GetObjectGlobalId()}");

            int targetX = _currentTarget.GetPosition().GetX();
            int targetY = _currentTarget.GetPosition().GetY();
            // 0 - тип атаки (обычная)
            ActivateSkill(targetX, targetY, 0);
            _attackCooldownTicks = 30; // кулдаун ~1 секунда
            _currentState = BotState.Attacking;
        }

       private void MoveTowardsTarget()
{
    if (_currentTarget == null) return;

    Console.WriteLine($"[DEBUG BOT] TELEPORTING to target {_currentTarget.GetObjectGlobalId()}");

    int targetX = _currentTarget.GetPosition().GetX();
    int targetY = _currentTarget.GetPosition().GetY();

    // Смещаемся, чтобы не оказаться прямо внутри цели
    int newX = targetX - 100;
    int newY = targetY - 100;

    SetPosition(newX, newY, GetZ());
    _currentState = BotState.MovingToTarget;
}
    }
}