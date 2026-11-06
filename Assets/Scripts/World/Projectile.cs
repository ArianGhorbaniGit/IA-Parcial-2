using UnityEngine;

//Proyectil del ataque a distancia: sigue al Boid objetivo y le hace daño al llegar
public class Projectile : MonoBehaviour
{
    [SerializeField]
    private float _speed = 18f;
    [SerializeField]
    private float _hitDistance = 0.35f;
    [SerializeField]
    private float _lifeTime = 3f;

    private Boid _target;
    private float _damage;
    private string _source;
    private float _timer;

    public void Launch(Boid target, float damage, string source)
    {
        _target = target;
        _damage = damage;
        _source = source;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_target == null || !_target.IsAlive || _timer > _lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = _target.Position + Vector3.up * 0.5f;
        Vector3 dir = targetPosition - transform.position;
        float step = _speed * Time.deltaTime;

        if (dir.magnitude <= step + _hitDistance)
        {
            _target.TakeDamage(_damage, _source + " (a distancia)");
            Destroy(gameObject);
            return;
        }

        transform.position += dir.normalized * step;
        transform.forward = dir.normalized;
    }
}
