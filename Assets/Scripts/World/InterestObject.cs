using System;
using UnityEngine;

//Objeto de interes que genera el Cazador. Los Boids llegan con Arrive y le van sacando vida hasta destruirlo.
public class InterestObject : MonoBehaviour
{
    [SerializeField]
    private float _maxHealth = 8f;
    [SerializeField, Tooltip("Radio del objeto: los Boids frenan antes de este borde")]
    private float _radius = 0.6f;
    [SerializeField]
    private Transform _visual;

    private Vector3 _initialScale = Vector3.one;
    private WorldLabel _label;

    public float Health { get; private set; }
    public float Radius => _radius;
    public bool IsAlive => Health > 0f;
    public Vector3 Position => transform.position.SetY(0f);

    public event Action<InterestObject> OnDestroyed = delegate { };

    private void Awake()
    {
        Health = _maxHealth;
        if (_visual != null) _initialScale = _visual.localScale;
        _label = GetComponent<WorldLabel>();
        if (TryGetComponent(out SphereCollider sphere)) sphere.radius = _radius;
        UpdateVisual();
    }

    public void TakeDamage(float damage, Boid eater)
    {
        if (!IsAlive) return;

        Health = Mathf.Max(0f, Health - damage);
        UpdateVisual();

        if (Health <= 0f)
        {
            EventLog.Add($"{name} destruido (lo termino {eater.name})", new Color(0.5f, 0.9f, 0.5f));
            OnDestroyed(this);
            Destroy(gameObject);
        }
    }

    private void UpdateVisual()
    {
        float percent = _maxHealth > 0f ? Health / _maxHealth : 0f;
        if (_visual != null)
        {
            float scale = Mathf.Lerp(0.35f, 1f, percent);
            _visual.localScale = new Vector3(_initialScale.x * scale, _initialScale.y, _initialScale.z * scale);
        }
        if (_label != null) _label.SetText($"{Health:0}/{_maxHealth:0}");
    }
}
