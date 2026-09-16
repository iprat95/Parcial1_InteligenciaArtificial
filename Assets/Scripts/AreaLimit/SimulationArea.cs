using UnityEngine;

public class SimulationArea : MonoBehaviour
{
    public static SimulationArea Instance { get; private set; }

    [SerializeField] private float width = 40f;
    [SerializeField] private float depth = 40f;
    [SerializeField] private float groundY = 0f;
    [SerializeField] private bool drawGizmo = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Vector3 GetRandomPoint()
    {
        float x = Random.Range(-width / 2f, width / 2f);
        float z = Random.Range(-depth / 2f, depth / 2f);
        return transform.position + new Vector3(x, groundY, z);
    }

    public Vector3 WrapPosition(Vector3 position)
    {
        Vector3 local = position - transform.position;
        float halfWidth = width / 2f;
        float halfDepth = depth / 2f;

        if (local.x > halfWidth) local.x = -halfWidth;
        else if (local.x < -halfWidth) local.x = halfWidth;

        if (local.z > halfDepth) local.z = -halfDepth;
        else if (local.z < -halfDepth) local.z = halfDepth;

        local.y = groundY;
        return transform.position + local;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * groundY, new Vector3(width, 0.05f, depth));
    }
}
