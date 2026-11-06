using UnityEngine;

//Parcial 2 - Search (estado inicial, reemplaza a Patrol).
//Fuera del Coto: A* hasta el Coto. Adentro: lo recorre yendo con A* a nodos del Coto al azar
//y cada cierto tiempo genera un objeto de interes (si hay menos de 5).
//Desde aca: Boid eliminado a la vista -> Gather; TBA listo y Boid vivo a la vista -> Attack
public class SearchState : State<HunterP2.States>
{
    private readonly HunterP2 _hunter;
    private float _spawnTimer;
    private bool _hasDestination;
    private string _destinationName = "-";

    public SearchState(FiniteStateMachine<HunterP2.States> fsm, HunterP2 hunter) : base(fsm, "SEARCH")
    {
        _hunter = hunter;
    }

    public override void Enter()
    {
        _hasDestination = false;
    }

    public override void Update()
    {
        if (_hunter.ClosestDeadBoid != null)
        {
            _fsm.ChangeState(HunterP2.States.Gather, "Boid eliminado dentro de VisionRadius");
            return;
        }

        if (_hunter.CanAttack && _hunter.ClosestAliveBoid != null)
        {
            _fsm.ChangeState(HunterP2.States.Attack, "Termino el TBA y hay un Boid vivo en VisionRadius");
            return;
        }

        bool inHuntingGround = _hunter.InHuntingGround;
        if (inHuntingGround)
        {
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _hunter.SpawnInterval)
            {
                _spawnTimer = 0f;
                _hunter.TrySpawnObject();
            }
        }

        if (!_hasDestination) ChooseDestination(inHuntingGround);
        if (_hunter.FollowPath(_hunter.MaxSpeed)) _hasDestination = false;

        _hunter.CurrentTarget = _destinationName;
        _hunter.CurrentAction = inHuntingGround
            ? "Recorriendo el Coto (A* a un nodo al azar)"
            : $"A* hacia el Coto de caza ({_hunter.PathFollower.PathLength:0}m)";
    }

    public override void Exit()
    {
        _hunter.ClearPath();
        _hunter.CurrentTarget = "-";
    }

    private void ChooseDestination(bool inHuntingGround)
    {
        Node node = _hunter.GetRandomHuntingNode(4f);
        if (node == null)
        {
            _hunter.Stop();
            return;
        }

        _destinationName = node.name;
        _hasDestination = _hunter.CalculatePathTo(node.Position, inHuntingGround ? node.name : "el Coto de caza", !inHuntingGround);
    }
}
