using System.Collections.Generic;
using UnityEngine;

public class PointOfInterest : MonoBehaviour
{
    
    public static readonly List<PointOfInterest> Active = new List<PointOfInterest>();

    [SerializeField] private float health = 100f;
    [SerializeField] private float drainPerTick = 10f;
    [SerializeField] private float drainInterval = 1f;
    [SerializeField] private float interactionRadius = 1.2f;
    [SerializeField] private int maxSimultaneousHarvesters = 2;

    public float InteractionRadius => interactionRadius;
    public bool IsAlive { get; private set; } = true;

    public bool HasCapacity => _harvesters.Count < maxSimultaneousHarvesters;

    private readonly Dictionary<BoidAgent, float> _drainTimers = new Dictionary<BoidAgent, float>();
    private readonly HashSet<BoidAgent> _harvesters = new HashSet<BoidAgent>();

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    public bool TryReserve(BoidAgent boid)
    {
        if (_harvesters.Contains(boid)) return true;
        if (_harvesters.Count >= maxSimultaneousHarvesters) return false;

        _harvesters.Add(boid);
        return true;
    }

    public void Release(BoidAgent boid)
    {
        _harvesters.Remove(boid);
    }
    public void Harvest(BoidAgent harvester, float deltaTime)
    {
        if (!IsAlive) return;

        if (!_drainTimers.ContainsKey(harvester)) _drainTimers[harvester] = 0f;
        _drainTimers[harvester] += deltaTime;

        if (_drainTimers[harvester] >= drainInterval)
        {
            _drainTimers[harvester] = 0f;
            health -= drainPerTick;

            if (health <= 0f)
            {
                IsAlive = false;
                Destroy(gameObject);
            }
        }
    }

    public static PointOfInterest FindClosest(Vector3 from, float maxRange = 0f)
    {
        PointOfInterest closest = null;
        float closestDist = float.MaxValue;

        foreach (var poi in Active)
        {
            if (poi == null || !poi.IsAlive || !poi.HasCapacity) continue;

            float d = Vector3.Distance(from, poi.transform.position);
            if (maxRange > 0f && d > maxRange) continue;

            if (d < closestDist)
            {
                closestDist = d;
                closest = poi;
            }
        }

        return closest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
