using UnityEngine;

//Busca alrededor de la ultima posicion conocida: va con A* a nodos cercanos y mira para los costados.
//Si no encuentra nada en SearchTime vuelve a patrullar.
public class WolfSearchState : State<Wolf.States>
{
    private readonly Wolf _wolf;
    private float _timer;
    private float _waitTimer;
    private bool _moving;

    public WolfSearchState(FiniteStateMachine<Wolf.States> fsm, Wolf wolf) : base(fsm, "SEARCH")
    {
        _wolf = wolf;
    }

    public override void Enter()
    {
        _timer = 0f;
        _waitTimer = 0.8f;
        _moving = false;
    }

    public override void Update()
    {
        if (_wolf.AlertReceived)
        {
            _fsm.ChangeState(Wolf.States.Alert, "Recibio una posicion nueva (aullido)");
            return;
        }

        _timer += Time.deltaTime;
        if (_timer >= _wolf.SearchTime)
        {
            _wolf.Stop();
            _fsm.ChangeState(Wolf.States.Patrol, "No lo encontro: vuelve a patrullar");
            return;
        }

        _wolf.Status = $"Buscando... {_wolf.SearchTime - _timer:0.0}s";

        if (_moving)
        {
            if (_wolf.FollowPath(_wolf.PatrolSpeed * 1.2f))
            {
                _moving = false;
                _waitTimer = 1f;
            }
            return;
        }

        _wolf.Stop();
        _wolf.LookAround(150f);
        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0f) GoToNextSpot();
    }

    private void GoToNextSpot()
    {
        Node node = _wolf.GetRandomNodeNear(_wolf.LastKnownPosition, _wolf.SearchRadius);
        _moving = node != null && _wolf.CalculatePathTo(node.Position, "punto de busqueda", false);
        if (!_moving) _waitTimer = 1f;
    }
}
