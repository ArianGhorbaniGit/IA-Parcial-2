using UnityEngine;

//Va con A* a la ultima posicion conocida del Cazador (la que recibio por aullido o donde lo vio por ultima vez).
//Si llega una alerta nueva mientras va, recalcula el camino.
public class WolfAlertState : State<Wolf.States>
{
    private readonly Wolf _wolf;
    private Vector3 _targetPosition;

    public WolfAlertState(FiniteStateMachine<Wolf.States> fsm, Wolf wolf) : base(fsm, "ALERTA")
    {
        _wolf = wolf;
    }

    public override void Enter()
    {
        CalculatePath();
    }

    public override void Update()
    {
        if (_wolf.AlertReceived) CalculatePath(); //posicion nueva

        bool pathFinished = _wolf.FollowPath(_wolf.AlertSpeed);
        float distance = _wolf.DistanceTo(_targetPosition);
        _wolf.Status = $"A* a ultima posicion ({distance:0.0}m)";

        if (pathFinished || distance <= 1.2f)
        {
            _wolf.Stop();
            _fsm.ChangeState(Wolf.States.Search, "Llego a la ultima posicion conocida: busca en la zona");
        }
    }

    private void CalculatePath()
    {
        _wolf.ConsumeAlert();
        _targetPosition = _wolf.LastKnownPosition;
        _wolf.CalculatePathTo(_targetPosition, $"la ultima posicion conocida ({_targetPosition.x:0}, {_targetPosition.z:0})", true);
    }
}
