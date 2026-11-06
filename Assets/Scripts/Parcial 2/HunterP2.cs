using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

//NPC Cazador del Parcial 2. Sale de la Zona inicial, va al Coto, caza y recolecta RequiredGathers Boids
//y despues va a la Salida, escapando de los lobos.
//Prioridad de estados: Dead > Flee > Exit > Gather > Attack > Search
public class HunterP2 : HunterBase
{
    public enum States { Search, Attack, Gather, Flee, Exit, Dead }

    [Header("Parcial 2 - Variables obligatorias")]
    [Tooltip("Recolecciones necesarias para ir a la Salida")]
    public int RequiredGathers = 3;
    [Tooltip("Tiempo que tarda en reaparecer en la Zona inicial cuando lo alcanza un lobo")]
    public float RespawnTime = 5f;
    [Tooltip("Tiempo que espera dentro de una Zona segura antes de volver a lo que estaba haciendo")]
    public float SafeZoneWaitTime = 4f;

    [Header("Navegacion")]
    [SerializeField]
    private LayerMask _wallMask;
    [SerializeField]
    private float _waypointCheckDistance = 0.7f;
    [SerializeField]
    private float _pathRadius = 0.3f;
    [SerializeField]
    private float _fleeSpeed = 5.6f;

    [Header("Zonas")]
    [SerializeField]
    private MapZone _startZone;
    [SerializeField]
    private Transform _spawnPoint;
    [SerializeField]
    private MapZone _huntingGround;
    [SerializeField]
    private MapZone[] _safeZones;
    [SerializeField]
    private Transform _exitPoint;

    [Header("Sensor de lobos")]
    [SerializeField, Tooltip("Mismo sensor que usan los lobos, con angulo 360 (distancia + linea de vision)")]
    private FieldOfView _wolfSensor;
    [SerializeField]
    private LayerMask _wolfMask;
    [SerializeField, Tooltip("Al huir, el A* evita los nodos que esten a menos de esta distancia de un lobo")]
    private float _wolfAvoidDistance = 6f;

    [Header("Feedback extra")]
    [SerializeField, Tooltip("Todos los renderers del cuerpo, para ocultarlo cuando muere o sale")]
    private Renderer[] _allRenderers;

    private readonly FiniteStateMachine<States> _fsm = new();
    private readonly Collider[] _wolfHits = new Collider[16];
    private readonly List<Wolf> _detectedWolves = new();
    private Collider _collider;
    private AgentTint _visibility;
    private LineIndicator _pathLine;
    private LineIndicator _wolfLine;
    private float _replanTimer;
    private Vector3 _lastTargetPosition = Vector3.positiveInfinity;
    private bool _fleeAvoidingWolves;

    private static readonly Color SearchColor = new(0.25f, 0.75f, 1f);
    private static readonly Color AttackColor = new(1f, 0.3f, 0.3f);
    private static readonly Color GatherColor = new(1f, 0.82f, 0.25f);
    private static readonly Color FleeColor = new(0.8f, 0.45f, 1f);
    private static readonly Color ExitColor = new(0.35f, 0.55f, 1f);
    private static readonly Color DeadColor = new(0.35f, 0.35f, 0.38f);

    public FiniteStateMachine<States> FSM => _fsm;
    public PathFollower PathFollower { get; private set; }
    public bool InSafeZone { get; private set; }
    public bool IsHidden { get; private set; }
    public bool CanBeDetected => !IsHidden && !InSafeZone; //en una Zona segura los lobos no lo pueden detectar
    public bool IsCaught { get; private set; }
    public string CaughtBy { get; private set; } = "-";
    public Wolf ClosestWolf { get; private set; }
    public MapZone ChosenSafeZone { get; private set; }
    public bool InHuntingGround => _huntingGround != null && _huntingGround.Contains(Position);
    public Vector3 ExitPosition => _exitPoint != null ? _exitPoint.position.SetY(0f) : Position;
    public float FleeSpeed => _fleeSpeed;
    public int CompletedRuns { get; private set; }
    public int Deaths { get; private set; }

    //En el Parcial 2, dentro de MeleeAttackRadius golpea directamente
    public override float MeleeHitDistance => MeleeAttackRadius;
    protected override string GatherText => $"{GatherCount}/{RequiredGathers}";

    protected override string StateName => _fsm.CurrentStateName;
    protected override string PreviousStateName => _fsm.PreviousStateName;
    protected override string LastReason => _fsm.LastReason;
    protected override float TimeInState => _fsm.TimeInState;
    protected override Color StateColor => _fsm.CurrentState switch
    {
        States.Attack => AttackColor,
        States.Gather => GatherColor,
        States.Flee => FleeColor,
        States.Exit => ExitColor,
        States.Dead => DeadColor,
        _ => SearchColor
    };

    protected override void Awake()
    {
        base.Awake();
        if (_wallMask.value == 0) _wallMask = LayerMask.GetMask("Obstacle");
        if (_wolfMask.value == 0) _wolfMask = LayerMask.GetMask("Wolf");
        if (_wolfSensor == null) _wolfSensor = GetComponent<FieldOfView>();

        _collider = GetComponent<Collider>();
        _visibility = new AgentTint(_allRenderers);
        _pathLine = new LineIndicator(transform, "Camino A*", 0.18f, new Color(0.4f, 0.9f, 1f, 0.9f), 0.22f);
        _wolfLine = new LineIndicator(transform, "Lobo detectado", 0.1f, new Color(1f, 0.3f, 0.3f, 0.9f), 0.4f);

        //Mismo grafo y mismo A* que los lobos, pero el Cazador puede usar todos los nodos
        PathFollower = new PathFollower(null, _wallMask, _pathRadius, _pathLine);
    }

    protected override void SetupFSM()
    {
        _fsm.AddState(States.Search, new SearchState(_fsm, this));
        _fsm.AddState(States.Attack, new AttackState<States>(_fsm, this, States.Search));
        _fsm.AddState(States.Gather, new GatherState<States>(_fsm, this, () => GatherCount >= RequiredGathers ? States.Exit : States.Search));
        _fsm.AddState(States.Flee, new FleeState(_fsm, this));
        _fsm.AddState(States.Exit, new ExitState(_fsm, this));
        _fsm.AddState(States.Dead, new DeadState(_fsm, this));

        //Transiciones con prioridad sobre todo lo demas (Dead y despues Flee)
        _fsm.AddGlobalTransition(States.Dead, () => IsCaught, "Lo alcanzo un lobo");
        _fsm.AddGlobalTransition(States.Flee, () => ClosestWolf != null, "Detecto un lobo", States.Dead);

        _fsm.OnStateChanged += (state, reason) => LogStateChange(reason);
        _fsm.ChangeState(States.Search, "Estado inicial");
    }

    protected override void UpdateFSM() => _fsm.Update();

    #region Sensores

    protected override bool CanSee(Vector3 position) => Vision.InLineOfSight(Position, position, _wallMask);

    protected override void UpdateOtherSensors()
    {
        InSafeZone = false;
        foreach (MapZone zone in _safeZones)
        {
            if (zone != null && zone.Contains(Position))
            {
                InSafeZone = true;
                break;
            }
        }

        _detectedWolves.Clear();
        ClosestWolf = null;
        if (IsHidden || _wolfSensor == null) return;

        float closest = float.MaxValue;
        int count = Physics.OverlapSphereNonAlloc(Position, _wolfSensor.ViewRadius, _wolfHits, _wolfMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Wolf wolf = _wolfHits[i].GetComponentInParent<Wolf>();
            if (wolf == null || !_wolfSensor.InFieldOfView(wolf.Position)) continue;

            _detectedWolves.Add(wolf);
            float distance = DistanceTo(wolf.Position);
            if (distance < closest)
            {
                closest = distance;
                ClosestWolf = wolf;
            }
        }
    }

    protected override LayerMask CollisionMask => _wallMask;

    #endregion

    #region Navegacion

    public bool CalculatePathTo(Vector3 destination, string description, bool log)
    {
        bool found = PathFollower.CalculatePath(Position, destination);
        if (log)
        {
            EventLog.Add(found
                ? $"{name} calcula A* hacia {description}: {PathFollower.NodeCount} nodos, {PathFollower.PathLength:0.0}m"
                : $"{name}: A* no encontro camino hacia {description}", new Color(0.5f, 0.9f, 1f));
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

    public void ClearPath() => PathFollower.Clear();

    //Ir a un Boid eliminado: si lo ve en linea recta usa Arrive (tramo final), si no usa A*
    public override void MoveTowards(Vector3 position, float stopDistance)
    {
        if (Vision.HasClearPath(Position, position, _pathRadius, _wallMask))
        {
            ClearPath();
            base.MoveTowards(position, stopDistance);
            return;
        }

        _replanTimer -= Time.deltaTime;
        bool needsPath = !PathFollower.HasPath || PathFollower.Destination.FlatDistance(position) > 1f;
        if (needsPath && _replanTimer <= 0f)
        {
            bool newTarget = _lastTargetPosition.FlatDistance(position) > 1f;
            CalculatePathTo(position, "el Boid eliminado", newTarget);
            _lastTargetPosition = position;
            _replanTimer = 0.5f;
        }

        if (FollowPath(_maxSpeed)) base.MoveTowards(position, stopDistance);
    }

    //Pursuit solo se usa con el objetivo a la vista (tramo final)
    public override void Pursue(SteeringAgent target)
    {
        ClearPath();
        base.Pursue(target);
    }

    public Node GetRandomHuntingNode(float minDistance)
    {
        List<Node> nodes = Graph.Current != null ? Graph.Current.GetNodesInZone(ZoneType.HuntingGround) : new();
        if (nodes.Count == 0) return null;

        for (int i = 0; i < 8; i++)
        {
            Node node = nodes[UnityEngine.Random.Range(0, nodes.Count)];
            if (DistanceTo(node.Position) >= minDistance) return node;
        }
        return nodes[UnityEngine.Random.Range(0, nodes.Count)];
    }

    //Elige la Zona segura mas cercana segun el largo del camino de A*.
    //Primero prueba con caminos que no pasen al lado del lobo; si no hay, usa el grafo completo.
    public MapZone ChooseSafeZone(out string comparison)
    {
        _fleeAvoidingWolves = true;
        MapZone best = FindClosestSafeZone(IsAwayFromWolves, out comparison);
        if (best == null)
        {
            _fleeAvoidingWolves = false;
            best = FindClosestSafeZone(null, out comparison);
        }

        SetChosenSafeZone(best);
        return best;
    }

    public void CalculateFleePath(MapZone zone)
    {
        bool found = PathFollower.CalculatePath(Position, zone.Center, _fleeAvoidingWolves ? (Func<Node, bool>)IsAwayFromWolves : null);
        if (!found && _fleeAvoidingWolves) found = PathFollower.CalculatePath(Position, zone.Center);

        EventLog.Add(found
            ? $"{name} calcula A* de huida a {zone.ZoneName}: {PathFollower.NodeCount} nodos, {PathFollower.PathLength:0.0}m{(_fleeAvoidingWolves ? " (evitando al lobo)" : "")}"
            : $"{name}: A* no encontro camino hacia {zone.ZoneName}", FleeColor);
    }

    private MapZone FindClosestSafeZone(Func<Node, bool> filter, out string comparison)
    {
        MapZone best = null;
        float bestLength = float.PositiveInfinity;
        StringBuilder sb = new();

        foreach (MapZone zone in _safeZones)
        {
            if (zone == null) continue;
            float length = PathFollower.GetPathLength(Position, zone.Center, filter);
            sb.Append(float.IsPositiveInfinity(length) ? $"{zone.ZoneName} sin camino  " : $"{zone.ZoneName} {length:0.0}m  ");
            if (length < bestLength)
            {
                bestLength = length;
                best = zone;
            }
        }

        comparison = sb.ToString().Trim();
        return best;
    }

    private bool IsAwayFromWolves(Node node)
    {
        foreach (Wolf wolf in _detectedWolves)
        {
            if (wolf != null && node.Position.FlatDistance(wolf.Position) < _wolfAvoidDistance) return false;
        }
        return true;
    }

    public void SetChosenSafeZone(MapZone zone)
    {
        if (ChosenSafeZone != null) ChosenSafeZone.SetHighlight(false, FleeColor);
        ChosenSafeZone = zone;
        if (ChosenSafeZone != null) ChosenSafeZone.SetHighlight(true, FleeColor);
    }

    #endregion

    #region Vida

    //Lo llama el lobo cuando lo alcanza
    public bool Catch(Wolf wolf)
    {
        if (IsCaught || !CanBeDetected) return false;
        IsCaught = true;
        CaughtBy = wolf.name;
        Deaths++;
        return true;
    }

    public void SetHidden(bool hidden)
    {
        IsHidden = hidden;
        _visibility.SetVisible(!hidden);
        if (_collider != null) _collider.enabled = !hidden;
        if (_label != null) _label.Hidden = hidden;
        if (hidden)
        {
            _wolfLine.Hide();
            _targetLine.Hide();
        }
    }

    //Reaparece en la Zona inicial con el contador de recolecciones en 0
    public void RespawnAtStart(string reason, bool completedRun)
    {
        if (completedRun) CompletedRuns++;
        transform.position = (_spawnPoint != null ? _spawnPoint.position : Position).SetY(0f);
        Stop();
        ClearPath();
        GatherCount = 0;
        IsCaught = false;
        SetHidden(false);
        string zoneName = _startZone != null ? _startZone.ZoneName : "Zona inicial";
        EventLog.Add($"{name} reaparece en la {zoneName} ({reason}). Recolecciones = 0", SearchColor);
    }

    #endregion

    #region Feedback

    protected override string GetLabelText()
    {
        string tba = CanAttack ? "TBA listo" : $"TBA {TBATimer:0.0}s";
        return $"{StateName}  [{GatherCount}/{RequiredGathers}]\n{CurrentAction}\n{tba}";
    }

    protected override void UpdateFeedback()
    {
        base.UpdateFeedback();

        if (!IsHidden && ClosestWolf != null) _wolfLine.SetLine(Position, ClosestWolf.Position);
        else _wolfLine.Hide();
    }

    public override void AppendHudInfo(StringBuilder sb)
    {
        base.AppendHudInfo(sb);

        string wolves = "";
        foreach (Wolf wolf in _detectedWolves) wolves += $"{wolf.name} ({DistanceTo(wolf.Position):0.0}m) ";
        sb.AppendLine(_detectedWolves.Count > 0 ? $"Lobos detectados: <color=#FF6A5A>{wolves}</color>" : "Lobos detectados: ninguno");
        sb.AppendLine($"Zona segura elegida: {(ChosenSafeZone != null ? ChosenSafeZone.ZoneName : "-")}   En zona segura: {(InSafeZone ? "SI" : "no")}");
        sb.AppendLine(PathFollower.HasPath
            ? $"Camino A*: {PathFollower.NodeCount} nodos, {PathFollower.PathLength:0.0}m"
            : "Camino A*: - (steering directo o quieto)");
        sb.AppendLine($"Objetivos cumplidos: {CompletedRuns}   Muertes: {Deaths}");
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (!_drawGizmos || !Application.isPlaying) return;

        Gizmos.color = Color.red;
        foreach (Wolf wolf in _detectedWolves)
        {
            if (wolf != null) Gizmos.DrawLine(transform.position + Vector3.up, wolf.transform.position + Vector3.up);
        }
    }

    #endregion
}
