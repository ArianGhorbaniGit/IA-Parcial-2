using UnityEngine;

//Patrulla por waypoints (Loop o PingPong, fijo para cada lobo).
//Si el proximo waypoint esta a la vista va derecho; si no hay linea de vision usa A*.
public class WolfPatrolState : State<Wolf.States>
{
    private readonly Wolf _wolf;
    private bool _routeChecked;
    private bool _usingPath;

    public WolfPatrolState(FiniteStateMachine<Wolf.States> fsm, Wolf wolf) : base(fsm, "PATROL")
    {
        _wolf = wolf;
    }

    public override void Enter()
    {
        _wolf.ForgetTarget();
        _routeChecked = false;
    }

    public override void Update()
    {
        if (_wolf.TargetInSafeZone || _wolf.TargetGone) _wolf.ForgetTarget();

        if (_wolf.AlertReceived)
        {
            _fsm.ChangeState(Wolf.States.Alert, "Recibio una alerta (aullido)");
            return;
        }

        PatrolData data = _wolf.PatrolData;
        if (!data.HasWaypoints)
        {
            _wolf.Stop();
            _wolf.Status = "Sin waypoints";
            return;
        }

        if (_wolf.DistanceTo(data.CurrentWaypoint.position) <= data.waypointCheckDistance)
        {
            data.NextWaypoint();
            _routeChecked = false;
        }

        Vector3 waypoint = data.CurrentWaypoint.position;
        if (!_routeChecked)
        {
            _usingPath = !_wolf.HasClearPathTo(waypoint);
            if (_usingPath) _wolf.CalculatePathTo(waypoint, $"{data.CurrentWaypoint.name} (sin linea de vision)", true);
            else _wolf.PathFollower.Clear();
            _routeChecked = true;
        }

        if (_usingPath)
        {
            if (_wolf.FollowPath(_wolf.PatrolSpeed)) _usingPath = false; //termino el camino, el ultimo tramo va derecho
            _wolf.Status = $"-> {data.CurrentWaypoint.name} (A*)";
        }
        else
        {
            _wolf.GoTowards(waypoint, _wolf.PatrolSpeed);
            _wolf.Status = $"-> {data.CurrentWaypoint.name}";
        }
    }
}
