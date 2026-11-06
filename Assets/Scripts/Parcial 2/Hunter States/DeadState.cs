using UnityEngine;

//Parcial 2 - Dead. Lo alcanzo un lobo: desaparece de la escena y no responde a nada.
//Pasado RespawnTime reaparece en la Zona inicial con el contador en 0 y vuelve a Search.
public class DeadState : State<HunterP2.States>
{
    private readonly HunterP2 _hunter;
    private float _timer;

    public DeadState(FiniteStateMachine<HunterP2.States> fsm, HunterP2 hunter) : base(fsm, "DEAD")
    {
        _hunter = hunter;
    }

    public override void Enter()
    {
        _timer = 0f;
        _hunter.ClearPath();
        _hunter.Stop();
        _hunter.SetHidden(true);
        _hunter.CurrentTarget = "-";
        EventLog.Add($"{_hunter.name} fue alcanzado por {_hunter.CaughtBy}: reaparece en {_hunter.RespawnTime:0.#}s", new Color(1f, 0.35f, 0.35f));
    }

    public override void Update()
    {
        _hunter.Stop();
        _timer += Time.deltaTime;
        _hunter.CurrentAction = $"Reaparece en {Mathf.Max(0f, _hunter.RespawnTime - _timer):0.0}s";

        if (_timer >= _hunter.RespawnTime)
        {
            _hunter.RespawnAtStart("despues de ser alcanzado", false);
            _fsm.ChangeState(HunterP2.States.Search, "Reaparecio en la Zona inicial");
        }
    }
}
