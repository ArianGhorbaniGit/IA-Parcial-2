using UnityEngine;

//Parcial 2 - Flee. Entra cuando detecta un lobo (transicion global, prioridad sobre todo menos Dead).
//Elige la Zona segura mas cercana por largo de camino A*, va con A* sin atacar ni recolectar,
//y adentro espera SafeZoneWaitTime. Despues vuelve a Search, o a Exit si ya tiene las recolecciones.
public class FleeState : State<HunterP2.States>
{
    private readonly HunterP2 _hunter;
    private MapZone _safeZone;
    private bool _isSafe;
    private float _waitTimer;

    public FleeState(FiniteStateMachine<HunterP2.States> fsm, HunterP2 hunter) : base(fsm, "FLEE")
    {
        _hunter = hunter;
    }

    public override void Enter()
    {
        _waitTimer = 0f;
        Wolf wolf = _hunter.ClosestWolf;

        if (_hunter.TryGetComponent(out WorldLabel label)) label.Flash("¡LOBO!", 1f);
        EventLog.Add($"{_hunter.name} detecta a {(wolf != null ? wolf.name : "un lobo")}: ¡huye!", new Color(0.8f, 0.45f, 1f));

        _safeZone = _hunter.ChooseSafeZone(out string comparison);
        if (_safeZone == null)
        {
            EventLog.Add($"{_hunter.name}: no hay Zona segura alcanzable", Color.red);
            return;
        }

        _hunter.CurrentTarget = _safeZone.ZoneName;
        EventLog.Add($"{_hunter.name} elige {_safeZone.ZoneName} (largo de camino A*: {comparison})", new Color(0.8f, 0.45f, 1f));

        _isSafe = _safeZone.Contains(_hunter.Position, 0.6f);
        _hunter.CalculateFleePath(_safeZone);
    }

    public override void Update()
    {
        if (_safeZone == null)
        {
            //Sin zona alcanzable: me alejo del lobo con steering, y cuando ya no lo veo sigo con lo mio
            if (_hunter.ClosestWolf == null)
            {
                _hunter.Stop();
                _fsm.ChangeState(_hunter.GatherCount >= _hunter.RequiredGathers ? HunterP2.States.Exit : HunterP2.States.Search, "Ya no ve al lobo");
                return;
            }
            _hunter.SeekPosition(_hunter.Position * 2f - _hunter.ClosestWolf.Position, _hunter.FleeSpeed);
            _hunter.CurrentAction = "Huyendo (sin Zona segura)";
            return;
        }

        //El camino termina en el centro de la Zona segura
        bool pathFinished = _hunter.FollowPath(_hunter.FleeSpeed);

        if (!_isSafe)
        {
            if (!_safeZone.Contains(_hunter.Position, 0.6f))
            {
                if (pathFinished) _hunter.CalculateFleePath(_safeZone); //lo desviaron: recalculo
                _hunter.CurrentAction = $"Huyendo a {_safeZone.ZoneName} (A*)";
                return;
            }

            _isSafe = true;
            EventLog.Add($"{_hunter.name} esta a salvo en {_safeZone.ZoneName}: espera {_hunter.SafeZoneWaitTime:0.#}s", new Color(0.8f, 0.45f, 1f));
        }

        if (pathFinished) _hunter.Stop();

        _waitTimer += Time.deltaTime;
        _hunter.CurrentAction = $"En {_safeZone.ZoneName}: espera {Mathf.Max(0f, _hunter.SafeZoneWaitTime - _waitTimer):0.0}s";

        if (_waitTimer >= _hunter.SafeZoneWaitTime)
        {
            if (_hunter.GatherCount >= _hunter.RequiredGathers) _fsm.ChangeState(HunterP2.States.Exit, "Termino la espera y ya tiene las recolecciones");
            else _fsm.ChangeState(HunterP2.States.Search, "Termino la espera en la Zona segura");
        }
    }

    public override void Exit()
    {
        _hunter.SetChosenSafeZone(null);
        _hunter.ClearPath();
        _hunter.CurrentTarget = "-";
    }
}
