using System.Collections.Generic;
using System.Text;
using UnityEngine;

//Base del NPC Cazador (Parcial 1 y Parcial 2): variables obligatorias, sensor de Boids,
//ataques, recoleccion, objetos de interes y feedback. Cada parcial arma su propia FSM.
public abstract class HunterBase : SteeringAgent, IHudInfo
{
    [Header("Variables obligatorias")]
    [Tooltip("Time Between Attacks: tiempo minimo entre ataques")]
    public float TBA = 3f;
    [Tooltip("Distancia maxima para hacer ataques a distancia")]
    public float RangeAttackRadius = 8f;
    [Tooltip("Distancia maxima para hacer ataques cuerpo a cuerpo")]
    public float MeleeAttackRadius = 3.5f;
    [Tooltip("Rango de percepcion de Boids")]
    public float VisionRadius = 12f;

    [Header("Ataque")]
    [SerializeField, Tooltip("Distancia a la que concreta el golpe cuerpo a cuerpo")]
    protected float _meleeHitDistance = 1.2f;
    [SerializeField]
    protected float _meleeDamage = 100f;
    [SerializeField]
    protected float _rangedDamage = 100f;
    [SerializeField]
    protected Projectile _projectilePrefab;

    [Header("Recoleccion")]
    [SerializeField, Tooltip("Cuanto tarda en recolectar un Boid eliminado")]
    protected float _gatherTime = 2.5f;
    [SerializeField]
    protected float _gatherDistance = 1.3f;

    [Header("Objetos de interes")]
    [SerializeField]
    protected InterestObject _interestObjectPrefab;
    [SerializeField, Tooltip("Cada cuanto intenta generar un objeto")]
    protected float _spawnInterval = 4f;
    [SerializeField, Tooltip("Maximo de objetos activos al mismo tiempo")]
    protected int _maxObjects = 5;
    [SerializeField, Tooltip("Zona donde aparecen los objetos")]
    protected FlockBounds _spawnArea;

    [Header("Percepcion")]
    [SerializeField]
    protected LayerMask _boidMask;

    [Header("Feedback")]
    [SerializeField]
    protected Renderer[] _renderers;
    [SerializeField]
    protected bool _drawGizmos = true;

    protected WorldLabel _label;
    protected AgentTint _tint;
    protected LineIndicator _targetLine;

    private readonly Collider[] _boidHits = new Collider[64];
    private readonly List<Boid> _detectedBoids = new();
    private readonly List<InterestObject> _objects = new();
    private float _tbaTimer;
    private int _objectCount;
    private Boid _ignoredBoid;
    private float _ignoreTimer;
    private float _flashTimer;

    public bool CanAttack => _tbaTimer <= 0f;
    public float TBATimer => _tbaTimer;
    public int GatherCount { get; protected set; }
    public Boid ClosestAliveBoid { get; private set; }
    public Boid ClosestDeadBoid { get; private set; }
    public List<Boid> DetectedBoids => _detectedBoids;
    public float SpawnInterval => _spawnInterval;
    public float GatherTime => _gatherTime;
    public float GatherDistance => _gatherDistance;
    public virtual float MeleeHitDistance => _meleeHitDistance;

    //Para el feedback
    public string CurrentTarget { get; set; } = "-";
    public string CurrentAction { get; set; } = "-";
    public Vector3? TargetPoint { get; set; }

    //Datos de la FSM de cada cazador (cada uno tiene su enum de estados)
    protected abstract string StateName { get; }
    protected abstract string PreviousStateName { get; }
    protected abstract string LastReason { get; }
    protected abstract float TimeInState { get; }
    protected abstract Color StateColor { get; }

    public virtual int HudOrder => 0;

    protected virtual void Awake()
    {
        _label = GetComponent<WorldLabel>();
        _tint = new AgentTint(_renderers);
        _targetLine = new LineIndicator(transform, "Linea objetivo", 0.12f, Color.white, 0.2f);
        if (_boidMask.value == 0) _boidMask = LayerMask.GetMask("Boid");
    }

    protected virtual void Start()
    {
        SetupFSM();
    }

    //Crea los estados y arranca la FSM
    protected abstract void SetupFSM();
    protected abstract void UpdateFSM();

    protected virtual void Update()
    {
        if (_tbaTimer > 0f) _tbaTimer -= Time.deltaTime;
        if (_ignoreTimer > 0f) _ignoreTimer -= Time.deltaTime;

        _speedLimit = _maxSpeed;
        UpdateBoidSensor();
        UpdateOtherSensors();

        UpdateFSM();

        Move();
        UpdateFeedback();
    }

    protected virtual void UpdateOtherSensors() { }

    #region Sensor de Boids

    //En el Parcial 2 ademas se pide linea de vision
    protected virtual bool CanSee(Vector3 position) => true;

    public bool IsInVision(Boid boid)
    {
        return boid != null && DistanceTo(boid.Position) <= VisionRadius && CanSee(boid.Position);
    }

    private void UpdateBoidSensor()
    {
        _detectedBoids.Clear();
        ClosestAliveBoid = null;
        ClosestDeadBoid = null;
        float closestAlive = float.MaxValue;
        float closestDead = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(Position, VisionRadius, _boidHits, _boidMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Boid boid = _boidHits[i].GetComponentInParent<Boid>();
            if (boid == null || (!boid.IsAlive && !boid.IsDead)) continue;
            if (boid == _ignoredBoid && boid.IsDead && _ignoreTimer > 0f) continue;

            float distance = DistanceTo(boid.Position);
            if (distance > VisionRadius || !CanSee(boid.Position)) continue;

            _detectedBoids.Add(boid);
            if (boid.IsAlive && distance < closestAlive)
            {
                closestAlive = distance;
                ClosestAliveBoid = boid;
            }
            else if (boid.IsDead && distance < closestDead)
            {
                closestDead = distance;
                ClosestDeadBoid = boid;
            }
        }
    }

    #endregion

    #region Movimiento (lo usan los estados)

    //En el Parcial 1 va directo con Arrive; en el Parcial 2 usa A* si no tiene linea de vision
    public virtual void MoveTowards(Vector3 position, float stopDistance)
    {
        ApplyForce(CalculateArrive(position, _maxSpeed, 2f, stopDistance));
        _speedLimit = _maxSpeed;
    }

    public virtual void Pursue(SteeringAgent target)
    {
        ApplyForce(CalculatePursuit(target, _maxSpeed));
        _speedLimit = _maxSpeed;
    }

    public void SeekPosition(Vector3 position, float speed)
    {
        ApplyForce(CalculateSeek(position, speed));
        _speedLimit = speed;
    }

    #endregion

    #region Acciones

    public void ResetTBA() => _tbaTimer = TBA;

    //Si no pudo llegar a un Boid eliminado, no lo vuelve a buscar por un rato
    public void IgnoreBoid(Boid boid, float time)
    {
        _ignoredBoid = boid;
        _ignoreTimer = time;
    }

    public void MeleeAttack(Boid target)
    {
        LookAt(target.Position);
        _flashTimer = 0.3f;
        if (_label != null) _label.Flash("¡GOLPE!", 0.8f);
        EventLog.Add($"{name}: ataque CUERPO A CUERPO a {target.name}", new Color(1f, 0.45f, 0.45f));
        target.TakeDamage(_meleeDamage, name + " (cuerpo a cuerpo)");
        ResetTBA();
    }

    public void RangedAttack(Boid target)
    {
        LookAt(target.Position);
        if (_label != null) _label.Flash("¡DISPARO!", 0.8f);
        EventLog.Add($"{name}: ataque A DISTANCIA a {target.name} ({DistanceTo(target.Position):0.0}m)", new Color(1f, 0.85f, 0.3f));

        if (_projectilePrefab != null)
        {
            Vector3 spawnPosition = transform.position + Vector3.up + transform.forward * 0.7f;
            Projectile projectile = Instantiate(_projectilePrefab, spawnPosition, transform.rotation);
            projectile.Launch(target, _rangedDamage, name);
        }
        else
        {
            target.TakeDamage(_rangedDamage, name + " (a distancia)");
        }
        ResetTBA();
    }

    public virtual void OnBoidGathered(Boid boid)
    {
        GatherCount++;
        EventLog.Add($"{name} recolecto a {boid.name} (total: {GatherCount})", new Color(1f, 0.85f, 0.25f));
    }

    //Genera un objeto de interes si hay menos que el maximo
    public void TrySpawnObject()
    {
        if (_interestObjectPrefab == null || _objects.Count >= _maxObjects) return;

        Vector3 position = _spawnArea != null ? _spawnArea.GetRandomPosition(_spawnArea.Margin + 0.5f) : Position + Random.insideUnitSphere * 5f;
        position.y = 0f;

        InterestObject interestObject = Instantiate(_interestObjectPrefab, position, Quaternion.identity);
        interestObject.name = $"Objeto {++_objectCount:00}";
        interestObject.OnDestroyed += obj => _objects.Remove(obj);
        _objects.Add(interestObject);

        EventLog.Add($"{name} genera {interestObject.name} en ({position.x:0}, {position.z:0}) [{_objects.Count}/{_maxObjects}]", new Color(0.5f, 1f, 0.6f));
    }

    #endregion

    #region Feedback

    protected void LogStateChange(string reason)
    {
        EventLog.Add($"{name}: {PreviousStateName} -> {StateName} ({reason})", StateColor);
    }

    protected virtual string GetLabelText()
    {
        string tba = CanAttack ? "TBA: listo" : $"TBA: {_tbaTimer:0.0}s";
        return $"{StateName}\n{CurrentAction}\n{tba}";
    }

    protected virtual string GatherText => GatherCount.ToString();

    protected virtual void UpdateFeedback()
    {
        _flashTimer -= Time.deltaTime;
        Color color = StateColor;
        _tint.SetColor(_flashTimer > 0f ? Color.white : color);

        if (_label != null)
        {
            _label.SetText(GetLabelText());
            _label.SetColor(Color.Lerp(color, Color.white, 0.5f));
        }

        if (TargetPoint.HasValue)
        {
            _targetLine.SetColor(new Color(color.r, color.g, color.b, 0.9f));
            _targetLine.SetLine(Position, TargetPoint.Value);
        }
        else
        {
            _targetLine.Hide();
        }
    }

    public virtual void AppendHudInfo(StringBuilder sb)
    {
        string hex = FeedbackResources.ToHex(StateColor);
        sb.AppendLine($"<b>CAZADOR</b>  <color=#{hex}><b>{StateName}</b></color>  ({TimeInState:0.0}s)");
        sb.AppendLine($"Ultima transicion: <i>{LastReason}</i>");
        sb.AppendLine($"Objetivo: {CurrentTarget}");
        sb.AppendLine($"Accion: {CurrentAction}");
        sb.AppendLine(CanAttack ? "TBA: <color=#7CFC7C>listo</color>" : $"TBA: {_tbaTimer:0.0}s / {TBA:0.0}s");
        sb.AppendLine($"Boids detectados: {_detectedBoids.Count} (vivo mas cercano: {GetName(ClosestAliveBoid)}, eliminado: {GetName(ClosestDeadBoid)})");
        sb.AppendLine($"Recolecciones: <b>{GatherText}</b>");
        sb.AppendLine($"Objetos de interes activos: {_objects.Count}/{_maxObjects}");
    }

    protected static string GetName(Component component) => component != null ? component.name : "-";

    protected virtual void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        Vector3 position = transform.position + Vector3.up * 0.06f;
        Gizmos.color = Color.white;
        GizmoUtils.DrawCircle(position, VisionRadius, 64);
        Gizmos.color = Color.yellow;
        GizmoUtils.DrawCircle(position, RangeAttackRadius, 56);
        Gizmos.color = Color.red;
        GizmoUtils.DrawCircle(position, MeleeAttackRadius, 40);

        if (!Application.isPlaying) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        foreach (Boid boid in _detectedBoids)
        {
            if (boid != null) Gizmos.DrawLine(position, boid.transform.position + Vector3.up * 0.06f);
        }
    }

    #endregion
}
