using UnityEngine;

//NPC Cazador del Parcial 1: FSM con Patrol, Attack y Gather
public class HunterP1 : HunterBase
{
    public enum States { Patrol, Attack, Gather }

    [Header("Patrulla")]
    [SerializeField]
    private PatrolData _patrolData = new();
    [SerializeField]
    private float _patrolSpeed = 4f;

    private readonly FiniteStateMachine<States> _fsm = new();

    private static readonly Color PatrolColor = new(0.25f, 0.75f, 1f);
    private static readonly Color AttackColor = new(1f, 0.3f, 0.3f);
    private static readonly Color GatherColor = new(1f, 0.82f, 0.25f);

    public FiniteStateMachine<States> FSM => _fsm;
    public PatrolData PatrolData => _patrolData;
    public float PatrolSpeed => _patrolSpeed;

    protected override string StateName => _fsm.CurrentStateName;
    protected override string PreviousStateName => _fsm.PreviousStateName;
    protected override string LastReason => _fsm.LastReason;
    protected override float TimeInState => _fsm.TimeInState;
    protected override Color StateColor => _fsm.CurrentState switch
    {
        States.Attack => AttackColor,
        States.Gather => GatherColor,
        _ => PatrolColor
    };

    protected override void SetupFSM()
    {
        _fsm.AddState(States.Patrol, new PatrolState(_fsm, this));
        _fsm.AddState(States.Attack, new AttackState<States>(_fsm, this, States.Patrol));
        _fsm.AddState(States.Gather, new GatherState<States>(_fsm, this, () => States.Patrol));

        _fsm.OnStateChanged += (state, reason) => LogStateChange(reason);
        _fsm.ChangeState(States.Patrol, "Estado inicial");
    }

    protected override void UpdateFSM() => _fsm.Update();

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (_drawGizmos) _patrolData.DrawGizmos(new Color(0.25f, 0.75f, 1f, 0.8f));
    }
}
