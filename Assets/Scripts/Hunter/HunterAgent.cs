using System.Collections.Generic;
using UnityEngine;

public enum HunterStates { Patrol, Attack, Gather }

public class HunterAgent : Agent, IHunterContext
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Patrol")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private float waypointArriveDistance = 0.5f;
    [SerializeField] private GameObject pointOfInterestPrefab;
    [SerializeField] private float poiSpawnInterval = 6f;
    [SerializeField] private int maxActivePois = 5;
    [SerializeField] private float trapPlacementDuration = 1.5f;

    [Header("Percepción")]
    [SerializeField] private float visionRadius = 10f;
    [SerializeField] private LayerMask boidLayer;

    [Header("TBA y Daño")]
    [SerializeField] private float tba = 2.5f;
    [SerializeField] private float rangeAttackRadius = 6f;
    [SerializeField] private float meleeAttackRadius = 1.5f;
    [SerializeField] private float meleeDamage = 34f;
    [SerializeField] private float rangeDamage = 20f;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Gather")]
    [SerializeField] private float gatherDuration = 2f;
    [SerializeField] private float gatherInteractDistance = 1f;

    public Transform Transform => transform;
    public float MoveSpeed => moveSpeed;
    public float RotationSpeed => rotationSpeed;
    public List<Transform> Waypoints => waypoints;
    public float WaypointArriveDistance => waypointArriveDistance;
    public GameObject PoiPrefab => pointOfInterestPrefab;
    public float PoiSpawnInterval => poiSpawnInterval;
    public int MaxActivePois => maxActivePois;
    public float TrapPlacementDuration => trapPlacementDuration;
    public float VisionRadius => visionRadius;
    public float TBA => tba;
    public float RangeAttackRadius => rangeAttackRadius;
    public float MeleeAttackRadius => meleeAttackRadius;
    public float GatherDuration => gatherDuration;
    public float GatherInteractDistance => gatherInteractDistance;

    public float AttackTimer { get; set; }
    public BoidAgent CurrentTarget { get; set; }

    public string CurrentStateName { get; private set; } = "-";
    public string LastAction { get; private set; } = "-";
    public int DetectedBoidsCount { get; private set; }

    private StateMachine _stateMachine;
    private static readonly Collider[] Hits = new Collider[32];

    private void Awake()
    {
        _stateMachine = new StateMachine();

        var patrol = new HunterPatrolState(this, _stateMachine);
        var attack = new HunterAttackState(this, _stateMachine);
        var gather = new HunterGatherState(this, _stateMachine);

        _stateMachine.RegisterState(HunterStates.Patrol, patrol);
        _stateMachine.RegisterState(HunterStates.Attack, attack);
        _stateMachine.RegisterState(HunterStates.Gather, gather);

        _stateMachine.ChangeState(HunterStates.Patrol);
    }

    private void Update()
    {
        AttackTimer -= Time.deltaTime;
        _stateMachine.Update();
        CurrentStateName = _stateMachine.CurrentState?.GetType().Name ?? "-";
    }

    public void SetLastAction(string text) => LastAction = text;
    public void MoveTowards(Vector3 targetPosition)
    {
        Vector3 dir = targetPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        _velocity = dir.normalized * moveSpeed;
        transform.position += _velocity * Time.deltaTime;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), rotationSpeed * Time.deltaTime);
    }

    public void SenseBoids(List<BoidAgent> aliveOut, List<BoidAgent> eliminatedOut)
    {
        aliveOut.Clear();
        eliminatedOut.Clear();

        int count = Physics.OverlapSphereNonAlloc(transform.position, visionRadius, Hits, boidLayer);
        for (int i = 0; i < count; i++)
        {
            var boid = Hits[i].GetComponentInParent<BoidAgent>();
            if (boid == null) continue;

            if (boid.IsEliminated) eliminatedOut.Add(boid);
            else aliveOut.Add(boid);
        }

        DetectedBoidsCount = aliveOut.Count + eliminatedOut.Count;
    }

    public BoidAgent FindNearest(List<BoidAgent> list)
    {
        BoidAgent nearest = null;
        float best = float.MaxValue;
        foreach (var b in list)
        {
            if (b == null) continue;
            float d = (b.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = b; }
        }
        return nearest;
    }

    public bool CanSpawnPointOfInterest() => PointOfInterest.Active.Count < maxActivePois;

    public void SpawnPointOfInterestAt(Vector3 position)
    {
        Vector3 pos = position;
        pos.y = transform.position.y;

        GameObject go = pointOfInterestPrefab != null
            ? Instantiate(pointOfInterestPrefab, pos, Quaternion.identity)
            : CreateFallbackPoi(pos);

        if (go.GetComponent<PointOfInterest>() == null) go.AddComponent<PointOfInterest>();

        SetLastAction("Deje una trampa");
    }

    private GameObject CreateFallbackPoi(Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.8f;
        go.name = "PointOfInterest (auto)";
        var col = go.GetComponent<Collider>();
        col.isTrigger = true;
        return go;
    }

    public void PerformMeleeAttack(BoidAgent target)
    {
        target.TakeDamage(meleeDamage);
        AttackTimer = tba;
        SetLastAction($"Ataque cuerpo a cuerpo a {target.name}");
    }

    public void PerformRangeAttack(BoidAgent target)
    {
        if (projectilePrefab != null)
        {
            var proj = Instantiate(projectilePrefab, transform.position + Vector3.up, Quaternion.identity);
            var hp = proj.GetComponent<HunterProjectile>();
            if (hp == null) hp = proj.AddComponent<HunterProjectile>();
            hp.Launch(target, rangeDamage);
        }
        else
        {
            target.TakeDamage(rangeDamage);
        }

        AttackTimer = tba;
        SetLastAction($"Ataque a distancia a {target.name}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeAttackRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, rangeAttackRadius);
    }
}
