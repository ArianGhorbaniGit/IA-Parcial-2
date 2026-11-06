using UnityEngine;

//Parcial 1 - Patrol: recorre los waypoints y cada cierto tiempo genera un objeto de interes (si hay menos de 5).
//Pasa a Gather si ve un Boid eliminado, o a Attack si termino el TBA y ve un Boid vivo.
public class PatrolState : State<HunterP1.States>
{
    private readonly HunterP1 _hunter;
    private float _spawnTimer;

    public PatrolState(FiniteStateMachine<HunterP1.States> fsm, HunterP1 hunter) : base(fsm, "PATROL")
    {
        _hunter = hunter;
    }

    public override void Enter()
    {
        _hunter.CurrentTarget = GetWaypointName();
    }

    public override void Update()
    {
        if (_hunter.ClosestDeadBoid != null)
        {
            _fsm.ChangeState(HunterP1.States.Gather, "Detecto un Boid eliminado");
            return;
        }

        if (_hunter.CanAttack && _hunter.ClosestAliveBoid != null)
        {
            _fsm.ChangeState(HunterP1.States.Attack, "Termino el TBA y hay un Boid en rango");
            return;
        }

        //El timer no se reinicia al salir de Patrol
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _hunter.SpawnInterval)
        {
            _spawnTimer = 0f;
            _hunter.TrySpawnObject();
        }

        Patrol();
    }

    private void Patrol()
    {
        PatrolData data = _hunter.PatrolData;
        if (!data.HasWaypoints)
        {
            _hunter.Stop();
            return;
        }

        Transform nextWaypoint = data.CurrentWaypoint;
        if (_hunter.DistanceTo(nextWaypoint.position) <= data.waypointCheckDistance)
        {
            data.NextWaypoint();
            nextWaypoint = data.CurrentWaypoint;
            _hunter.CurrentTarget = GetWaypointName();
        }

        _hunter.TargetPoint = nextWaypoint.position;
        _hunter.SeekPosition(nextWaypoint.position, _hunter.PatrolSpeed);
        _hunter.CurrentAction = $"Patrullando ({data.mode})";
    }

    public override void Exit()
    {
        _hunter.TargetPoint = null;
    }

    private string GetWaypointName()
    {
        Transform waypoint = _hunter.PatrolData.CurrentWaypoint;
        return waypoint != null ? waypoint.name : "-";
    }
}
