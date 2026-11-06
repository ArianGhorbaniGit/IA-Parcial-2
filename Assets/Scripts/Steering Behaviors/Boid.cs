using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Agente autonomo de la bandada. Decide solo con lo que ven sus sensores (OverlapSphere de rango limitado),
//sin lista global de agentes ni lider.
//Prioridad: Evade (si ve al Cazador) > Arrive al objeto de interes > Flocking
public class Boid : SteeringAgent
{
    public enum BoidStates { Flocking, Evade, Arrive, Eating, Dead, Collected }

    [Header("Rangos de percepcion")]
    [SerializeField, Tooltip("Rango de Separation (tiene que ser menor que el de Alignment/Cohesion)")]
    private float _separationRadius = 1.4f;
    [SerializeField, Tooltip("Rango de Alignment y Cohesion")]
    private float _viewRadius = 4.5f;
    [SerializeField, Tooltip("Vision: rango en el que detecta al Cazador y a los objetos de interes")]
    private float _detectionRadius = 6.5f;
    [SerializeField]
    private LayerMask _boidMask;
    [SerializeField]
    private LayerMask _hunterMask;
    [SerializeField]
    private LayerMask _foodMask;

    [Header("Flocking values")]
    [SerializeField, Range(0f, 3f)]
    private float _separationWeight = 2f;
    [SerializeField, Range(0f, 3f)]
    private float _alignmentWeight = 1f;
    [SerializeField, Range(0f, 3f)]
    private float _cohesionWeight = 1f;
    [SerializeField, Range(0f, 3f)]
    private float _boundsWeight = 2.5f;

    [Header("Evade")]
    [SerializeField]
    private float _evadeSpeed = 5f;
    [SerializeField, Range(0f, 3f)]
    private float _evadeWeight = 2f;

    [Header("Objetos de interes")]
    [SerializeField]
    private float _arriveRadius = 2.5f;
    [SerializeField, Tooltip("Cada cuanto le saca vida al objeto")]
    private float _eatInterval = 0.5f;
    [SerializeField]
    private float _eatDamage = 1f;

    [Header("Vida")]
    [SerializeField]
    private float _maxHealth = 100f;
    [SerializeField, Tooltip("Tiempo que tarda en reaparecer despues de ser recolectado")]
    private float _respawnTime = 4f;

    [Header("References")]
    [SerializeField]
    private FlockBounds _bounds;
    [SerializeField]
    private Renderer[] _renderers;
    [SerializeField]
    private bool _drawGizmos = true;

    private static readonly Color FlockingColor = new(0.55f, 0.82f, 1f);
    private static readonly Color EvadeColor = new(1f, 0.55f, 0.15f);
    private static readonly Color ArriveColor = new(0.35f, 0.9f, 0.45f);
    private static readonly Color EatingColor = new(0.75f, 1f, 0.3f);
    private static readonly Color DeadColor = new(0.25f, 0.25f, 0.28f);

    private readonly Collider[] _hits = new Collider[64];
    private readonly List<Boid> _separationNeighbors = new();
    private readonly List<Boid> _flockNeighbors = new();
    private SteeringAgent _threat;
    private InterestObject _food;
    private float _eatTimer;
    private float _wanderAngle;
    private float _hitFlashTimer;

    private Collider _collider;
    private AgentTint _tint;
    private WorldLabel _label;

    public BoidStates CurrentState { get; private set; } = BoidStates.Flocking;
    public float Health { get; private set; }
    public bool IsAlive => CurrentState != BoidStates.Dead && CurrentState != BoidStates.Collected;
    public bool IsDead => CurrentState == BoidStates.Dead;

    public void Setup(FlockBounds bounds) => _bounds = bounds;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _tint = new AgentTint(_renderers);
        _label = GetComponent<WorldLabel>();
        Health = _maxHealth;

        if (_boidMask.value == 0) _boidMask = LayerMask.GetMask("Boid");
        if (_hunterMask.value == 0) _hunterMask = LayerMask.GetMask("Hunter");
        if (_foodMask.value == 0) _foodMask = LayerMask.GetMask("Interest");
    }

    private void Start()
    {
        Vector3 randomVector = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        _velocity = randomVector.normalized * _maxSpeed * 0.6f;
        _wanderAngle = Random.Range(-180f, 180f);
        UpdateFeedback();
    }

    private void OnValidate()
    {
        //Consigna: Separation tiene que usar un rango menor al de Alignment/Cohesion
        if (_separationRadius >= _viewRadius) _separationRadius = _viewRadius * 0.5f;
    }

    private void Update()
    {
        if (!IsAlive) return; //muerto: queda quieto en el lugar

        UpdateSensors();
        _speedLimit = _maxSpeed;

        if (_threat != null)
        {
            CurrentState = BoidStates.Evade;
            _speedLimit = _evadeSpeed;
            _eatTimer = 0f;
            ApplyForce(CalculateEvade(_threat, _evadeSpeed), _evadeWeight);
            ApplyForce(CalculateSeparation(_evadeSpeed), _separationWeight);
        }
        else if (_food != null)
        {
            float stopDistance = _food.Radius + _radius + 0.15f;
            ApplyForce(CalculateArrive(_food.Position, _maxSpeed, _arriveRadius, stopDistance));
            ApplyForce(CalculateSeparation(_maxSpeed), _separationWeight * 0.5f);

            if (DistanceTo(_food.Position) <= stopDistance + 0.3f)
            {
                CurrentState = BoidStates.Eating;
                Eat();
            }
            else
            {
                CurrentState = BoidStates.Arrive;
                _eatTimer = 0f;
            }
        }
        else
        {
            CurrentState = BoidStates.Flocking;
            _eatTimer = 0f;
            CalculateFlocking();
        }

        if (_bounds != null)
        {
            Vector3 inward = _bounds.GetInwardDirection(Position);
            if (inward != Vector3.zero) ApplyForce(CalculateSteering(inward * _speedLimit), _boundsWeight);
        }

        Move();
        if (CurrentState == BoidStates.Eating && _food != null) LookAt(_food.Position);

        AvoidOverlapping();
        KeepInsideBounds();
        UpdateFeedback();
    }

    #region Sensores

    private void UpdateSensors()
    {
        _separationNeighbors.Clear();
        _flockNeighbors.Clear();
        _threat = null;
        _food = null;

        float closestThreat = float.MaxValue;
        float closestFood = float.MaxValue;
        int mask = _boidMask.value | _hunterMask.value | _foodMask.value;
        int count = Physics.OverlapSphereNonAlloc(Position, _detectionRadius, _hits, mask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            int layer = 1 << _hits[i].gameObject.layer;

            if ((_boidMask.value & layer) != 0)
            {
                Boid other = _hits[i].GetComponentInParent<Boid>();
                if (other == null || other == this || other.CurrentState == BoidStates.Collected) continue;

                float distance = DistanceTo(other.Position);
                if (distance <= _separationRadius) _separationNeighbors.Add(other); //vivos y muertos ocupan lugar
                if (other.IsAlive && distance <= _viewRadius) _flockNeighbors.Add(other);
            }
            else if ((_hunterMask.value & layer) != 0)
            {
                SteeringAgent hunter = _hits[i].GetComponentInParent<SteeringAgent>();
                if (hunter == null) continue;
                float distance = DistanceTo(hunter.Position);
                if (distance < closestThreat)
                {
                    closestThreat = distance;
                    _threat = hunter;
                }
            }
            else if ((_foodMask.value & layer) != 0)
            {
                InterestObject food = _hits[i].GetComponentInParent<InterestObject>();
                if (food == null || !food.IsAlive) continue;
                float distance = DistanceTo(food.Position);
                if (distance < closestFood)
                {
                    closestFood = distance;
                    _food = food;
                }
            }
        }
    }

    #endregion

    #region Flocking

    private void CalculateFlocking()
    {
        ApplyForce(CalculateSeparation(_maxSpeed), _separationWeight);
        ApplyForce(CalculateAlignment(), _alignmentWeight);
        ApplyForce(CalculateCohesion(), _cohesionWeight);

        //Sin vecinos (o casi quieto) deambula para no quedarse parado
        if (_flockNeighbors.Count == 0 || _velocity.magnitude < _maxSpeed * 0.5f) ApplyForce(CalculateWander());
    }

    private Vector3 CalculateSeparation(float speed)
    {
        Vector3 desired = default;
        int count = 0;

        foreach (Boid item in _separationNeighbors)
        {
            desired += item.Position - Position;
            count++;
        }

        if (count == 0) return Vector3.zero;

        desired /= count;
        if (desired.sqrMagnitude < 0.0001f) desired = transform.right;

        return CalculateSteering(-desired.normalized * speed);
    }

    private Vector3 CalculateAlignment()
    {
        Vector3 desired = default;
        int count = 0;

        foreach (Boid item in _flockNeighbors)
        {
            desired += item.Velocity;
            count++;
        }

        if (count == 0) return Vector3.zero;
        desired /= count;

        return CalculateSteering(desired.normalized * _maxSpeed);
    }

    private Vector3 CalculateCohesion()
    {
        Vector3 desiredPosition = default;
        int count = 0;

        foreach (Boid item in _flockNeighbors)
        {
            desiredPosition += item.Position;
            count++;
        }

        if (count == 0) return Vector3.zero;

        desiredPosition /= count;
        return CalculateSeek(desiredPosition, _maxSpeed);
    }

    private Vector3 CalculateWander()
    {
        _wanderAngle += Random.Range(-1f, 1f) * 180f * Time.deltaTime;
        Vector3 forward = _velocity.sqrMagnitude > 0.01f ? _velocity.normalized : transform.forward;
        Vector3 target = Position + forward * 2f + Quaternion.Euler(0f, _wanderAngle, 0f) * forward * 1.2f;
        return CalculateSeek(target, _maxSpeed);
    }

    #endregion

    private void Eat()
    {
        _eatTimer += Time.deltaTime;
        while (_eatTimer >= _eatInterval && _food != null && _food.IsAlive)
        {
            _eatTimer -= _eatInterval;
            _food.TakeDamage(_eatDamage, this);
        }
    }

    //Correccion de posicion para que nunca queden dos cuerpos superpuestos
    private void AvoidOverlapping()
    {
        foreach (Boid item in _separationNeighbors)
        {
            float minDistance = _radius + item.Radius;
            Vector3 diff = Position - item.Position;
            float distance = diff.magnitude;
            if (distance >= minDistance) continue;
            if (distance < 0.001f) diff = transform.right;

            float share = item.IsAlive ? 0.5f : 1f; //uno muerto no se mueve, me corro yo
            transform.position += diff.normalized * (minDistance - distance) * share;
        }

        if (_food != null)
        {
            float minDistance = _food.Radius + _radius;
            Vector3 diff = Position - _food.Position;
            float distance = diff.magnitude;
            if (distance < minDistance && distance > 0.001f) transform.position += diff.normalized * (minDistance - distance);
        }
    }

    private void KeepInsideBounds()
    {
        if (_bounds == null || _bounds.Contains(Position, _radius)) return;
        transform.position = _bounds.ClampPosition(Position, _radius);
    }

    #region Vida

    public void TakeDamage(float damage, string source)
    {
        if (!IsAlive) return;

        Health = Mathf.Max(0f, Health - damage);
        _hitFlashTimer = 0.25f;

        if (Health <= 0f)
        {
            Die(source);
            return;
        }

        if (_label != null) _label.Flash($"-{damage:0}", 0.8f);
        EventLog.Add($"{name} recibe {damage:0} de daño ({source}). Vida {Health:0}/{_maxHealth:0}", EvadeColor);
    }

    private void Die(string source)
    {
        CurrentState = BoidStates.Dead;
        Stop();
        _food = null;
        _threat = null;
        _tint.SetColor(DeadColor);
        if (_label != null)
        {
            _label.SetText("ELIMINADO");
            _label.Flash("X", 1f);
        }
        EventLog.Add($"{name} ELIMINADO por {source}: queda quieto en el lugar", new Color(1f, 0.4f, 0.4f));
    }

    //Lo llama el Cazador cuando termina de recolectarlo
    public void Collect()
    {
        if (!IsDead) return;

        CurrentState = BoidStates.Collected;
        _tint.SetVisible(false);
        _collider.enabled = false;
        if (_label != null) _label.SetText("");
        EventLog.Add($"{name} recolectado: desaparece y reaparece en {_respawnTime:0.#}s", new Color(0.75f, 0.75f, 0.8f));
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnTime);

        Vector3 spawn = _bounds != null ? _bounds.GetRandomPosition(_bounds.Margin) : Position;
        transform.position = spawn;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        _velocity = transform.forward * _maxSpeed * 0.5f;

        Health = _maxHealth;
        CurrentState = BoidStates.Flocking;
        _tint.SetVisible(true);
        _collider.enabled = true;
        UpdateFeedback();
        EventLog.Add($"{name} reaparece en ({spawn.x:0}, {spawn.z:0})", FlockingColor);
    }

    #endregion

    #region Feedback

    private void UpdateFeedback()
    {
        _hitFlashTimer -= Time.deltaTime;
        _tint.SetColor(_hitFlashTimer > 0f ? Color.white : GetStateColor(CurrentState));

        if (_label == null) return;
        string text = CurrentState switch
        {
            BoidStates.Evade => "EVADE",
            BoidStates.Arrive => "ARRIVE",
            BoidStates.Eating => "COMIENDO",
            _ => ""
        };
        if (Health < _maxHealth) text = text.Length > 0 ? $"{text} {Health:0}hp" : $"{Health:0}hp";
        _label.SetText(text);
    }

    public static Color GetStateColor(BoidStates state) => state switch
    {
        BoidStates.Evade => EvadeColor,
        BoidStates.Arrive => ArriveColor,
        BoidStates.Eating => EatingColor,
        BoidStates.Dead => DeadColor,
        _ => FlockingColor
    };

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || CurrentState == BoidStates.Collected) return;

        Vector3 position = transform.position + Vector3.up * 0.05f;
        Gizmos.color = Color.red;
        GizmoUtils.DrawCircle(position, _separationRadius, 20);
        Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.6f);
        GizmoUtils.DrawCircle(position, _viewRadius, 32);
        Gizmos.color = new Color(1f, 0.92f, 0.3f, 0.35f);
        GizmoUtils.DrawCircle(position, _detectionRadius, 40);

        if (!Application.isPlaying || !IsAlive) return;

        Gizmos.color = Color.cyan;
        foreach (Boid item in _flockNeighbors)
        {
            if (item != null) Gizmos.DrawLine(position, item.transform.position + Vector3.up * 0.05f);
        }
        if (_threat != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(position, _threat.transform.position);
        }
        if (_food != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(position, _food.transform.position);
        }
    }

    #endregion
}
