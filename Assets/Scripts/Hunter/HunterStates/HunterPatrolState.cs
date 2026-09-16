using System.Collections.Generic;
using UnityEngine;

public class HunterPatrolState : State
{
    private readonly IHunterContext _agent;
    private int _currentWaypointIndex = -1;
    private float _spawnTimer;

    private bool _isPlacingTrap;
    private float _placeTrapTimer;

    private readonly List<BoidAgent> _alive = new List<BoidAgent>();
    private readonly List<BoidAgent> _eliminated = new List<BoidAgent>();

    public HunterPatrolState(IHunterContext agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _spawnTimer = 0f;
        _isPlacingTrap = false;
        _agent.SetLastAction("Patrullando");
    }

    public override void Update()
    {
        if (_isPlacingTrap)
        {
            UpdateTrapPlacement();
            return;
        }

        PatrolMovement();

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _agent.PoiSpawnInterval && _agent.CanSpawnPointOfInterest())
        {
            _spawnTimer = 0f;
            StartPlacingTrap();
            return;
        }

        _agent.SenseBoids(_alive, _eliminated);

        if (_eliminated.Count > 0)
        {
            _agent.CurrentTarget = _agent.FindNearest(_eliminated);
            StateMachine.ChangeState(HunterStates.Gather);
            return;
        }

        if (_alive.Count > 0 && _agent.AttackTimer <= 0f)
        {
            _agent.CurrentTarget = _agent.FindNearest(_alive);
            StateMachine.ChangeState(HunterStates.Attack);
        }
    }

    private void StartPlacingTrap()
    {
        _isPlacingTrap = true;
        _placeTrapTimer = 0f;
        _agent.SetLastAction("Colocando una trampa...");
    }

    private void UpdateTrapPlacement()
    {
        _placeTrapTimer += Time.deltaTime;

        if (_placeTrapTimer >= _agent.TrapPlacementDuration)
        {
            _agent.SpawnPointOfInterestAt(_agent.Transform.position);
            _isPlacingTrap = false;
        }
    }

    private void PatrolMovement()
    {
        var waypoints = _agent.Waypoints;
        if (waypoints == null || waypoints.Count == 0) return;

        if (_currentWaypointIndex < 0 || _currentWaypointIndex >= waypoints.Count)
            PickNewRandomWaypoint(waypoints.Count);

        Transform target = waypoints[_currentWaypointIndex];
        _agent.MoveTowards(target.position);

        if (Vector3.Distance(_agent.Transform.position, target.position) <= _agent.WaypointArriveDistance)
        {
            PickNewRandomWaypoint(waypoints.Count);
        }
    }

    private void PickNewRandomWaypoint(int count)
    {
        if (count <= 1)
        {
            _currentWaypointIndex = 0;
            return;
        }

        int newIndex;
        do
        {
            newIndex = Random.Range(0, count);
        } while (newIndex == _currentWaypointIndex);

        _currentWaypointIndex = newIndex;
    }
}
