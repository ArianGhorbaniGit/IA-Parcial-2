using UnityEngine;

//Parcial 2 - Exit. Con las recolecciones completas va a la Salida con A* (sin atacar ni recolectar).
//Al llegar desaparece y reaparece en la Zona inicial con el contador en 0, y vuelve a Search.
public class ExitState : State<HunterP2.States>
{
    private const float ExitTime = 1.5f;

    private readonly HunterP2 _hunter;
    private bool _arrived;
    private float _timer;

    public ExitState(FiniteStateMachine<HunterP2.States> fsm, HunterP2 hunter) : base(fsm, "EXIT")
    {
        _hunter = hunter;
    }

    public override void Enter()
    {
        _arrived = false;
        _timer = 0f;
        _hunter.CurrentTarget = "Salida";
        EventLog.Add($"{_hunter.name} completo {_hunter.GatherCount}/{_hunter.RequiredGathers} recolecciones: va a la Salida", new Color(0.35f, 0.55f, 1f));
        _hunter.CalculatePathTo(_hunter.ExitPosition, "la Salida", true);
    }

    public override void Update()
    {
        if (!_arrived)
        {
            bool pathFinished = _hunter.FollowPath(_hunter.MaxSpeed);
            if (pathFinished && _hunter.DistanceTo(_hunter.ExitPosition) > 1.2f)
            {
                _hunter.CalculatePathTo(_hunter.ExitPosition, "la Salida", false); //lo desviaron: recalculo
            }

            if (_hunter.DistanceTo(_hunter.ExitPosition) > 1.2f)
            {
                _hunter.CurrentAction = $"A* hacia la Salida ({_hunter.DistanceTo(_hunter.ExitPosition):0}m)";
                return;
            }

            _arrived = true;
            _hunter.Stop();
            _hunter.ClearPath();
            _hunter.SetHidden(true);
            EventLog.Add($"{_hunter.name} llego a la Salida: objetivo cumplido", new Color(0.35f, 0.55f, 1f));
        }

        _hunter.Stop();
        _timer += Time.deltaTime;
        _hunter.CurrentAction = "Saliendo del mapa...";

        if (_timer >= ExitTime)
        {
            _hunter.RespawnAtStart("completo el objetivo", true);
            _fsm.ChangeState(HunterP2.States.Search, "Objetivo cumplido: reinicia el ciclo");
        }
    }

    public override void Exit()
    {
        _hunter.ClearPath();
        _hunter.CurrentTarget = "-";
    }
}
