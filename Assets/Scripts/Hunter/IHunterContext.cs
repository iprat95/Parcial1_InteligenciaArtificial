using System.Collections.Generic;
using UnityEngine;

public interface IHunterContext
{
    Transform Transform { get; }
    void MoveTowards(Vector3 targetPosition);

    void SetLastAction(string text);

    List<Transform> Waypoints { get; }
    float WaypointArriveDistance { get; }
    float PoiSpawnInterval { get; }
    float TrapPlacementDuration { get; }
    bool CanSpawnPointOfInterest();
    void SpawnPointOfInterestAt(Vector3 position);

    void SenseBoids(List<BoidAgent> aliveOut, List<BoidAgent> eliminatedOut);
    BoidAgent FindNearest(List<BoidAgent> list);
    float VisionRadius { get; }

    float AttackTimer { get; }
    float MeleeAttackRadius { get; }
    float RangeAttackRadius { get; }
    void PerformMeleeAttack(BoidAgent target);
    void PerformRangeAttack(BoidAgent target);

    float GatherInteractDistance { get; }
    float GatherDuration { get; }

    BoidAgent CurrentTarget { get; set; }
}
