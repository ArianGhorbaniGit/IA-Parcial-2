//Persigue al Cazador (Pursuit) mientras lo tenga en su campo de vision.
//Al entrar aulla para avisar a los demas, y mientras lo ve vuelve a aullar cada tanto con la posicion nueva.
public class WolfChaseState : State<Wolf.States>
{
    private readonly Wolf _wolf;

    public WolfChaseState(FiniteStateMachine<Wolf.States> fsm, Wolf wolf) : base(fsm, "CHASE")
    {
        _wolf = wolf;
    }

    public override void Enter()
    {
        EventLog.Add($"{_wolf.name} DETECTA al Cazador", Wolf.ChaseColor);
        if (_wolf.CanHowl) _wolf.Howl();
    }

    public override void Update()
    {
        if (_wolf.LostSight)
        {
            _fsm.ChangeState(Wolf.States.Alert, "Lo perdio de vista: va con A* a la ultima posicion conocida");
            return;
        }

        HunterP2 hunter = _wolf.VisibleHunter;
        if (hunter == null)
        {
            //Lo perdio hace un instante: sigue hacia donde lo vio
            _wolf.GoTowards(_wolf.LastKnownPosition, _wolf.ChaseSpeed);
            _wolf.Status = "¿Donde esta?";
            return;
        }

        _wolf.ChaseHunter(hunter);
        _wolf.Status = $"¡Persiguiendo! ({_wolf.DistanceTo(hunter.Position):0.0}m)";

        if (_wolf.CanHowl) _wolf.Howl();
    }
}
