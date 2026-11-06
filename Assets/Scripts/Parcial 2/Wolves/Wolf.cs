using System.Collections.Generic;
using UnityEngine;

//Lobo: patrulla, detecta al Cazador con su Field of View, avisa a los demas con un aullido y lo persigue.
//Guarda la ULTIMA posicion conocida del Cazador (solo cambia cuando lo ve o escucha un aullido).
public class Wolf : SteeringAgent
{
    public enum States { Patrol, Chase, Alert, Search }

    [Header("Velocidades (todas menores que la del Cazador)")]
    [SerializeField]
    private float _patrolSpeed = 2.6f;
    [SerializeField]
    private float _alertSpeed = 3.8f;
    [SerializeField]
    private float _chaseSpeed = 4.4f;

    [Header("Patrulla")]
    [SerializeField]
    private PatrolData _patrolData = new();

    [Header("Sensores")]
    [SerializeField]
    private FieldOfView _fieldOfView;
    [SerializeField]
    private LayerMask _hunterMask;
    [SerializeField, Tooltip("Lo que el lobo no puede atravesar: paredes y la barrera de las Zonas seguras")]
    private LayerMask _blockingMask;
    [SerializeField, Tooltip("Tiempo sin verlo antes de darlo por perdido (evita que parpadee en las esquinas)")]
    private float _lostSightTime = 0.4f;

    [Header("Aullido")]
    [SerializeField]
    private LayerMask _wolfMask;
    [SerializeField, Tooltip("Hasta donde se escucha el aullido")]
    private float _howlRadius = 120f;
    [SerializeField, Tooltip("Mientras lo ve, vuelve a aullar con la posicion nueva cada X segundos")]
    private float _howlCooldown = 2.5f;

    [Header("Busqueda")]
    [SerializeField]
    private float _searchTime = 7f;
    [SerializeField]
    private float _searchRadius = 8f;

    [Header("Captura")]
    [SerializeField, Tooltip("Si el Cazador queda a menos de esta distancia, lo alcanza")]
    private float _catchRadius = 1.1f;

    [Header("Navegacion")]
    [SerializeField]
    private float _waypointCheckDistance = 0.8f;
    [SerializeField]
    private float _pathRadius = 0.3f;
    [SerializeField]
    private float _separationRadius = 1.6f;

    [Header("Feedback")]
    [SerializeField]
    private Renderer[] _renderers;
    [SerializeField, Tooltip("Color de este lobo (ruta, camino y ultima posicion conocida)")]
    private Color _wolfColor = Color.white;
    [SerializeField]
    private bool _drawGizmos = true;

    public static readonly Color PatrolColor = new(0.62f, 0.64f, 0.68f);
    public static readonly Color ChaseColor = new(1f, 0.25f, 0.2f);
    public static readonly Color AlertColor = new(1f, 0.6f, 0.1f);
    public static readonly Color SearchColor = new(1f, 0.88f, 0.2f);

    private readonly FiniteStateMachine<States> _fsm = new();
    private readonly Collider[] _hits = new Collider[16];
    private float _lastSeenTime = -999f;
    private float _lastHowlTime = -999f;
    private float _howlAnimation = -1f;
    private AgentTint _tint;
    private WorldLabel _label;
    private LineIndicator _pathLine;
    private LineIndicator _memoryLine;
    private LineIndicator _memoryCircle;
    private LineIndicator _howlCircle;

    public FiniteStateMachine<States> FSM => _fsm;
    public PathFollower PathFollower { get; private set; }
    public PatrolData PatrolData => _patrolData;
    public float PatrolSpeed => _patrolSpeed;
    public float AlertSpeed => _alertSpeed;
    public float ChaseSpeed => _chaseSpeed;
    public float SearchTime => _searchTime;
    public float SearchRadius => _searchRadius;
    public Color WolfColor => _wolfColor;
    public string Status { get; set; } = "";

    //Lo que percibe y recuerda
    public HunterP2 VisibleHunter { get; private set; }
    public bool CanSeeHunter => VisibleHunter != null;
    public bool LostSight => !CanSeeHunter && Time.time - _lastSeenTime > _lostSightTime;
    public HunterP2 KnownTarget { get; private set; }
    public bool HasLastKnownPosition { get; private set; }
    public Vector3 LastKnownPosition { get; private set; }
    public bool AlertReceived { get; private set; }
    public bool CanHowl => Time.time - _lastHowlTime >= _howlCooldown;
    public bool TargetInSafeZone => KnownTarget != null && KnownTarget.InSafeZone && !KnownTarget.IsHidden;
    public bool TargetGone => KnownTarget != null && KnownTarget.IsHidden;

    private void Awake()
    {
        if (_hunterMask.value == 0) _hunterMask = LayerMask.GetMask("Hunter");
        if (_wolfMask.value == 0) _wolfMask = LayerMask.GetMask("Wolf");
        if (_blockingMask.value == 0) _blockingMask = LayerMask.GetMask("Obstacle", "SafeZone");
        if (_fieldOfView == null) _fieldOfView = GetComponent<FieldOfView>();

        _tint = new AgentTint(_renderers);
        _label = GetComponent<WorldLabel>();
        _pathLine = new LineIndicator(transform, "Camino A*", 0.14f, _wolfColor, 0.25f);
        _memoryLine = new LineIndicator(transform, "Linea ultima posicion", 0.06f, _wolfColor, 0.3f);
        _memoryCircle = new LineIndicator(transform, "Ultima posicion conocida", 0.12f, _wolfColor, 0.3f);
        _howlCircle = new LineIndicator(transform, "Aullido", 0.15f, AlertColor, 0.35f);

        //Mismo grafo y A* que el Cazador, pero sin los nodos de las Zonas seguras
        //(tampoco entra a la Zona inicial, que es el area aislada donde reaparece el Cazador)
        PathFollower = new PathFollower(CanUseNode, _blockingMask, _pathRadius, _pathLine);
    }

    private void Start()
    {
        _fsm.AddState(States.Patrol, new WolfPatrolState(_fsm, this));
        _fsm.AddState(States.Chase, new WolfChaseState(_fsm, this));
        _fsm.AddState(States.Alert, new WolfAlertState(_fsm, this));
        _fsm.AddState(States.Search, new WolfSearchState(_fsm, this));

        _fsm.AddGlobalTransition(States.Patrol, () => TargetInSafeZone, "El Cazador entro a una Zona segura: pierde el objetivo y vuelve a patrullar", States.Patrol);
        _fsm.AddGlobalTransition(States.Patrol, () => TargetGone, "El Cazador desaparecio: vuelve a patrullar", States.Patrol);
        _fsm.AddGlobalTransition(States.Chase, () => CanSeeHunter, "Detecto al Cazador (FOV)");

        _fsm.OnStateChanged += (state, reason) =>
            EventLog.Add($"{name}: {_fsm.PreviousStateName} -> {_fsm.CurrentStateName} ({reason})", GetStateColor(state));
        _fsm.ChangeState(States.Patrol, "Estado inicial");
    }

    private void Update()
    {
        _speedLimit = _patrolSpeed;

        UpdateSensor();
        _fsm.Update();
        CheckCatch();

        ApplyForce(CalculateSeparation());
        Move();

        UpdateFeedback();
    }

    protected override LayerMask CollisionMask => _blockingMask;

    public static bool CanUseNode(Node node) => !node.IsSafeZone && node.Zone != ZoneType.Start;

    #region Sensores

    private void UpdateSensor()
    {
        VisibleHunter = null;
        int count = Physics.OverlapSphereNonAlloc(Position, _fieldOfView.ViewRadius, _hits, _hunterMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            HunterP2 hunter = _hits[i].GetComponentInParent<HunterP2>();
            if (hunter == null || !hunter.CanBeDetected) continue;

            if (_fieldOfView.InFieldOfView(hunter.Position))
            {
                VisibleHunter = hunter;
                _lastSeenTime = Time.time;
                Remember(hunter, hunter.Position);
                return;
            }
        }
    }

    private void Remember(HunterP2 hunter, Vector3 position)
    {
        KnownTarget = hunter;
        LastKnownPosition = position;
        HasLastKnownPosition = true;
    }

    private void CheckCatch()
    {
        int count = Physics.OverlapSphereNonAlloc(Position, _catchRadius + 0.5f, _hits, _hunterMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            HunterP2 hunter = _hits[i].GetComponentInParent<HunterP2>();
            if (hunter == null || DistanceTo(hunter.Position) > _catchRadius) continue;

            if (hunter.Catch(this))
            {
                if (_label != null) _label.Flash("¡ATRAPADO!", 1.5f);
                EventLog.Add($"{name} ALCANZA al Cazador", ChaseColor);
            }
        }
    }

    #endregion

    #region Aullido

    //Le pasa la ultima posicion conocida del Cazador a los lobos que lo escuchan
    public void Howl()
    {
        if (!HasLastKnownPosition || KnownTarget == null) return;

        _lastHowlTime = Time.time;
        _howlAnimation = 0f;
        if (_label != null) _label.Flash("¡AUUU!", 1.2f);

        int listeners = 0;
        int count = Physics.OverlapSphereNonAlloc(Position, _howlRadius, _hits, _wolfMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Wolf other = _hits[i].GetComponentInParent<Wolf>();
            if (other == null || other == this) continue;
            other.HearHowl(this, LastKnownPosition, KnownTarget);
            listeners++;
        }

        EventLog.Add($"{name} AULLA: Cazador en ({LastKnownPosition.x:0}, {LastKnownPosition.z:0}) -> lo escuchan {listeners} lobos", AlertColor);
    }

    public void HearHowl(Wolf from, Vector3 position, HunterP2 hunter)
    {
        if (hunter == null || !hunter.CanBeDetected) return;

        bool wasCalm = !AlertReceived && !_fsm.IsCurrent(States.Alert) && !_fsm.IsCurrent(States.Chase);
        Remember(hunter, position);
        if (!CanSeeHunter) AlertReceived = true;

        if (_label != null) _label.Flash($"? alerta de {from.name}", 1.2f);
        if (wasCalm) EventLog.Add($"{name} recibe la alerta de {from.name}: va a ({position.x:0}, {position.z:0})", new Color(1f, 0.8f, 0.4f));
    }

    public void ConsumeAlert() => AlertReceived = false;

    public void ForgetTarget()
    {
        KnownTarget = null;
        HasLastKnownPosition = false;
        AlertReceived = false;
        PathFollower.Clear();
    }

    #endregion

    #region Movimiento (lo usan los estados)

    public bool CalculatePathTo(Vector3 destination, string description, bool log)
    {
        bool found = PathFollower.CalculatePath(Position, destination);
        if (log)
        {
            EventLog.Add(found
                ? $"{name} usa A* hacia {description}: {PathFollower.NodeCount} nodos, {PathFollower.PathLength:0.0}m"
                : $"{name}: A* no encontro camino hacia {description}", _wolfColor);
        }
        return found;
    }

    //Sigue el camino de A*. Devuelve true cuando llego al final
    public bool FollowPath(float speed)
    {
        if (!PathFollower.GetNextPoint(Position, _waypointCheckDistance, out Vector3 nextPoint, out bool isLast)) return true;

        ApplyForce(isLast ? CalculateArrive(nextPoint, speed, 1.5f) : CalculateSeek(nextPoint, speed));
        _speedLimit = speed;
        return false;
    }

    public bool HasClearPathTo(Vector3 position) => Vision.HasClearPath(Position, position, _pathRadius, _blockingMask);

    public void GoTowards(Vector3 position, float speed)
    {
        PathFollower.Clear();
        ApplyForce(CalculateArrive(position, speed, 1.5f));
        _speedLimit = speed;
    }

    public void ChaseHunter(HunterP2 hunter)
    {
        PathFollower.Clear();
        ApplyForce(CalculatePursuit(hunter, _chaseSpeed));
        _speedLimit = _chaseSpeed;
    }

    public void LookAround(float degreesPerSecond)
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }

    public Node GetRandomNodeNear(Vector3 center, float radius)
    {
        if (Graph.Current == null) return null;

        List<Node> candidates = new();
        foreach (Node node in Graph.Current.GetAllNodes)
        {
            if (node != null && CanUseNode(node) && node.Position.FlatDistance(center) <= radius) candidates.Add(node);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    //Separacion entre lobos: empuja hacia el costado para que se esquiven en los pasillos sin frenarse
    private Vector3 CalculateSeparation()
    {
        Vector3 away = default;
        int count = Physics.OverlapSphereNonAlloc(Position, _separationRadius, _hits, _wolfMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Wolf other = _hits[i].GetComponentInParent<Wolf>();
            if (other == null || other == this) continue;
            away += Position - other.Position;
        }
        if (away == Vector3.zero) return Vector3.zero;

        Vector3 side = Vector3.Cross(Vector3.up, transform.forward).normalized;
        float sideAmount = Vector3.Dot(away.normalized, side);
        if (Mathf.Abs(sideAmount) < 0.1f) sideAmount = 1f; //de frente: los dos se corren a su derecha
        return side * Mathf.Sign(sideAmount) * _maxForce * 0.6f * Time.deltaTime;
    }

    #endregion

    #region Feedback

    public static Color GetStateColor(States state) => state switch
    {
        States.Chase => ChaseColor,
        States.Alert => AlertColor,
        States.Search => SearchColor,
        _ => PatrolColor
    };

    private void UpdateFeedback()
    {
        Color color = GetStateColor(_fsm.CurrentState);
        _tint.SetColor(color);
        if (_fieldOfView != null) _fieldOfView.SetConeColor(new Color(color.r, color.g, color.b, CanSeeHunter ? 0.6f : 0.35f));

        if (_label != null)
        {
            _label.SetText($"{name}: {_fsm.CurrentStateName}\n{Status}");
            _label.SetColor(Color.Lerp(color, Color.white, 0.4f));
        }

        //Ultima posicion conocida que esta usando el lobo
        if (HasLastKnownPosition && !_fsm.IsCurrent(States.Patrol))
        {
            _memoryCircle.SetCircle(LastKnownPosition, 0.7f, 16);
            _memoryLine.SetLine(Position, LastKnownPosition);
        }
        else
        {
            _memoryCircle.Hide();
            _memoryLine.Hide();
        }

        //Anillo que se agranda cuando aulla
        if (_howlAnimation >= 0f)
        {
            _howlAnimation += Time.deltaTime;
            float t = _howlAnimation / 0.9f;
            if (t >= 1f)
            {
                _howlAnimation = -1f;
                _howlCircle.Hide();
            }
            else
            {
                _howlCircle.SetColor(new Color(AlertColor.r, AlertColor.g, AlertColor.b, 1f - t));
                _howlCircle.SetCircle(Position, Mathf.Lerp(0.8f, 9f, t), 40);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;
        _patrolData.DrawGizmos(new Color(_wolfColor.r, _wolfColor.g, _wolfColor.b, 0.7f));

        Gizmos.color = Color.red;
        GizmoUtils.DrawCircle(transform.position + Vector3.up * 0.05f, _catchRadius, 20);

        if (!Application.isPlaying || !HasLastKnownPosition) return;
        Gizmos.color = _wolfColor;
        Gizmos.DrawWireSphere(LastKnownPosition + Vector3.up * 0.3f, 0.6f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.3f, LastKnownPosition + Vector3.up * 0.3f);
    }

    #endregion
}
