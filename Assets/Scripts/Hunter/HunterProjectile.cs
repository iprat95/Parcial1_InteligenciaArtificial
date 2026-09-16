using UnityEngine;

public class HunterProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 24f;
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private float hitRadius = 1f;

    private Vector3 _direction;
    private BoidAgent _target;
    private float _damage;
    private bool _launched;

    public void Launch(BoidAgent target, float damage)
    {
        _target = target;
        _damage = damage;

        Vector3 aimPoint = target != null
            ? target.transform.position + Vector3.up * 0.5f
            : transform.position + transform.forward;

        _direction = (aimPoint - transform.position).normalized;
        _launched = true;
    }

    private void Update()
    {
        if (!_launched) return;

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += _direction * speed * Time.deltaTime;

        if (_target != null && !_target.IsEliminated)
        {
            float dist = Vector3.Distance(transform.position, _target.transform.position);
            if (dist <= hitRadius)
            {
                _target.TakeDamage(_damage);
                Destroy(gameObject);
            }
        }
    }
}
