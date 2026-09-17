using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidAgent : Agent
{
    private enum BoidState { Flocking, SeekingPoi, Harvesting, Evading, Eliminated, Collected }

    [Header("Movimiento")]
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float maxSteering = 8f;

    [Header("Flocking")]
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float alignmentRadius = 4f;
    [SerializeField] private float cohesionRadius = 4f;
    [SerializeField, Range(0f, 3f)] private float separationWeight = 1.6f;
    [SerializeField, Range(0f, 3f)] private float alignmentWeight = 1f;
    [SerializeField, Range(0f, 3f)] private float cohesionWeight = 1f;

    [Header("Evade")]
    [SerializeField] private float hunterVisionRadius = 8f;
    [SerializeField, Range(0f, 4f)] private float evadeWeight = 2.5f;

    [Header("Arrive")]
    [SerializeField] private float poiDetectionRadius = 15f;
    [SerializeField] private float arriveSlowingDistance = 3f;
    [SerializeField, Range(0f, 1f)] private float arriveMinSpeedFactor = 0.35f;
    [SerializeField] private float trapmateSeparationRadius = 0.6f;

    [Header("Vida")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private float respawnDelay = 5f;

    [Header("Feedback visual")]
    [SerializeField] private bool colorFeedback = true;
    [SerializeField] private Color flockingColor = new Color(0.2f, 0.4f, 1f);
    [SerializeField] private Color evadingColor = Color.red;
    [SerializeField] private Color harvestingColor = Color.green;
    [SerializeField] private Color eliminatedColor = Color.gray;

    public bool IsEliminated => _state == BoidState.Eliminated || _state == BoidState.Collected;
    public bool IsAvailableToGather => _state == BoidState.Eliminated;
    public string DebugState => _state.ToString();

    public static readonly List<BoidAgent> Active = new List<BoidAgent>();

    private float _currentHealth;
    private BoidState _state = BoidState.Flocking;
    private PointOfInterest _targetPoi;
    private Transform _hunterThreat;
    private Vector3 _hunterVelocity;

    private Renderer[] _renderers;
    private Collider _collider;
    private MaterialPropertyBlock _mpb;
    private Coroutine _respawnRoutine;

    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

    private void Awake()
    {
        _currentHealth = maxHealth;
        _collider = GetComponent<Collider>();
        _renderers = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();

        Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
        _velocity = randomDir * maxSpeed * 0.5f;
    }

    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);

    private void Update()
    {
        if (IsEliminated)
        {
            _velocity = Vector3.zero;
            UpdateColorFeedback();
            return;
        }

        DetectHunter();
        ResolveHighLevelState();

        if (_state == BoidState.Harvesting)
        {
            _velocity = Vector3.zero;
            _targetPoi?.Harvest(this, Time.deltaTime);
            if (_targetPoi == null)
            {
                _state = BoidState.Flocking;
                _velocity = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * maxSpeed * 0.5f;
            }
        }
        else
        {
            Vector3 steering;
            if (_state == BoidState.Evading) steering = ComputeEvadeSteering();
            else if (_state == BoidState.SeekingPoi) steering = ComputeArriveSteering();
            else steering = ComputeFlockingSteering();

            _velocity += steering;
            _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);

            Vector3 nextPos = transform.position + _velocity * Time.deltaTime;
            transform.position = SimulationArea.Instance != null ? SimulationArea.Instance.WrapPosition(nextPos) : nextPos;

            if (_velocity.sqrMagnitude > 0.0001f)
                transform.forward = _velocity.normalized;
        }

        UpdateColorFeedback();
    }

    private void ResolveHighLevelState()
    {
        if (_hunterThreat != null)
        {
            if (_state == BoidState.SeekingPoi || _state == BoidState.Harvesting)
                ReleaseCurrentPoi();

            _state = BoidState.Evading;
            return;
        }

        if (_state == BoidState.Harvesting) return;

        if (_targetPoi == null)
        {
            var candidate = PointOfInterest.FindClosest(transform.position, poiDetectionRadius);
            if (candidate != null && candidate.TryReserve(this))
                _targetPoi = candidate;
        }

        if (_targetPoi != null)
        {
            float dist = Vector3.Distance(transform.position, _targetPoi.transform.position);
            _state = dist <= _targetPoi.InteractionRadius ? BoidState.Harvesting : BoidState.SeekingPoi;
        }
        else
        {
            _state = BoidState.Flocking;
        }
    }

    private void ReleaseCurrentPoi()
    {
        if (_targetPoi != null)
        {
            _targetPoi.Release(this);
            _targetPoi = null;
        }
    }

    private void DetectHunter()
    {
        _hunterThreat = null;
        _hunterVelocity = Vector3.zero;

        float closestDist = hunterVisionRadius;

        foreach (var hunter in HunterAgent.Active)
        {
            if (hunter == null) continue;

            float dist = Vector3.Distance(transform.position, hunter.transform.position);
            if (dist <= closestDist)
            {
                closestDist = dist;
                _hunterThreat = hunter.transform;
                _hunterVelocity = hunter.Velocity;
            }
        }
    }

    private bool InRange(Vector3 pos, float radius) => (pos - transform.position).sqrMagnitude <= radius * radius;

    private List<BoidAgent> SenseNeighbors(float radius)
    {
        var list = new List<BoidAgent>();
        foreach (var other in Active)
        {
            if (other == null || other == this || other.IsEliminated) continue;
            if (InRange(other.transform.position, radius)) list.Add(other);
        }
        return list;
    }


    private Vector3 ComputeFlockingSteering()
    {
        return CalculateSeparation() * separationWeight
             + CalculateAlignment() * alignmentWeight
             + CalculateCohesion() * cohesionWeight;
    }

    private Vector3 CalculateSeparation()
    {
        var neighbors = SenseNeighbors(separationRadius);
        if (neighbors.Count == 0) return Vector3.zero;

        Vector3 desired = Vector3.zero;
        foreach (var n in neighbors) desired += transform.position - n.transform.position;
        desired /= neighbors.Count;

        return CalculateSteering(desired.normalized * maxSpeed);
    }

    private Vector3 CalculateSeparationWhileSeekingPoi()
    {
        var neighbors = SenseNeighbors(separationRadius);
        if (neighbors.Count == 0) return Vector3.zero;

        Vector3 desired = Vector3.zero;
        int count = 0;

        foreach (var n in neighbors)
        {
            bool sameTrap = _targetPoi != null && n._targetPoi == _targetPoi;
            float radius = sameTrap ? trapmateSeparationRadius : separationRadius;

            if (Vector3.Distance(transform.position, n.transform.position) > radius) continue;

            desired += transform.position - n.transform.position;
            count++;
        }

        if (count == 0) return Vector3.zero;
        desired /= count;

        return CalculateSteering(desired.normalized * maxSpeed);
    }

    private Vector3 CalculateAlignment()
    {
        var neighbors = SenseNeighbors(alignmentRadius);
        if (neighbors.Count == 0) return Vector3.zero;

        Vector3 desired = Vector3.zero;
        foreach (var n in neighbors) desired += n.Velocity;
        desired /= neighbors.Count;

        return CalculateSteering(desired.normalized * maxSpeed);
    }

    private Vector3 CalculateCohesion()
    {
        var neighbors = SenseNeighbors(cohesionRadius);
        if (neighbors.Count == 0) return Vector3.zero;

        Vector3 center = Vector3.zero;
        foreach (var n in neighbors) center += n.transform.position;
        center /= neighbors.Count;

        return Seek(center);
    }

    private Vector3 ComputeEvadeSteering()
    {
        if (_hunterThreat == null) return Vector3.zero;

        float distance = Vector3.Distance(transform.position, _hunterThreat.position);
        float predictionTime = distance / Mathf.Max(maxSpeed + _hunterVelocity.magnitude, 0.001f);
        Vector3 futurePos = _hunterThreat.position + _hunterVelocity * predictionTime;

        Vector3 desired = (transform.position - futurePos).normalized * maxSpeed;
        Vector3 evade = CalculateSteering(desired);

        return evade * evadeWeight + CalculateSeparation() * 0.5f;
    }

    private Vector3 ComputeArriveSteering()
    {
        if (_targetPoi == null) return Vector3.zero;

        return Arrive(_targetPoi.transform.position, _targetPoi.InteractionRadius) + CalculateSeparationWhileSeekingPoi() * 0.3f;
    }

    private Vector3 Seek(Vector3 target)
    {
        Vector3 desired = (target - transform.position).normalized * maxSpeed;
        return CalculateSteering(desired);
    }

    private Vector3 Arrive(Vector3 target, float minDistance)
    {
        Vector3 direction = target - transform.position;
        float distance = direction.magnitude;

        if (distance <= minDistance) return Vector3.zero;

        float t = Mathf.Clamp01((distance - minDistance) / arriveSlowingDistance);
        float targetSpeed = maxSpeed * Mathf.Max(t, arriveMinSpeedFactor);
        Vector3 desired = direction.normalized * targetSpeed;

        return CalculateSteering(desired);
    }

    private Vector3 CalculateSteering(Vector3 desired)
    {
        Vector3 steering = desired - _velocity;
        return Vector3.ClampMagnitude(steering, maxSteering * Time.deltaTime);
    }

    public void TakeDamage(float amount)
    {
        if (IsEliminated) return;

        _currentHealth -= amount;
        if (_currentHealth <= 0f)
        {
            _currentHealth = 0f;
            ReleaseCurrentPoi();
            _state = BoidState.Eliminated;
            _velocity = Vector3.zero;
        }
    }

    public void OnGathered()
    {
        _state = BoidState.Collected;

        if (_respawnRoutine != null) StopCoroutine(_respawnRoutine);
        _respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        SetVisible(false);
        yield return new WaitForSeconds(respawnDelay);

        transform.position = SimulationArea.Instance != null ? SimulationArea.Instance.GetRandomPoint() : transform.position;
        _currentHealth = maxHealth;
        ReleaseCurrentPoi();
        _hunterThreat = null;
        _velocity = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * maxSpeed * 0.5f;
        _state = BoidState.Flocking;

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in _renderers) r.enabled = visible;
        if (_collider != null) _collider.enabled = visible;
    }


    private void UpdateColorFeedback()
    {
        if (!colorFeedback || _renderers == null || _renderers.Length == 0) return;

        Color c;
        if (_state == BoidState.Evading) c = evadingColor;
        else if (_state == BoidState.Harvesting) c = harvestingColor;
        else if (IsEliminated) c = eliminatedColor;
        else c = flockingColor;

        foreach (var r in _renderers)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            _mpb.SetColor(ColorIdLegacy, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, separationRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, alignmentRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hunterVisionRadius);
    }
}