using System;
using UnityEngine;

//Estado Gather (lo usan los dos parciales). Va hasta el Boid eliminado, lo recolecta durante GatherTime
//y el Boid desaparece. Si el objetivo deja de estar disponible abandona la accion.
public class GatherState<T> : State<T>
{
    //Si en este tiempo no llega al Boid, lo da por no disponible
    private const float MaxApproachTime = 12f;

    private readonly HunterBase _hunter;
    private readonly Func<T> _getNextState;
    private Boid _target;
    private float _gatherTimer;
    private float _approachTimer;
    private bool _completed;

    public GatherState(FiniteStateMachine<T> fsm, HunterBase hunter, Func<T> getNextState) : base(fsm, "GATHER")
    {
        _hunter = hunter;
        _getNextState = getNextState;
    }

    public override void Enter()
    {
        _gatherTimer = 0f;
        _approachTimer = 0f;
        _completed = false;
        _target = _hunter.ClosestDeadBoid;

        if (_target == null)
        {
            _fsm.ChangeState(_getNextState(), "No hay Boid eliminado para recolectar");
            return;
        }

        _hunter.CurrentTarget = _target.name + " (eliminado)";
        EventLog.Add($"{_hunter.name} va a recolectar a {_target.name}", new Color(1f, 0.85f, 0.3f));
    }

    public override void Update()
    {
        if (_target == null || !_target.IsDead)
        {
            EventLog.Add($"{_hunter.name}: el Boid ya no esta disponible, abandona la recoleccion", new Color(0.8f, 0.8f, 0.8f));
            _fsm.ChangeState(_getNextState(), "Objetivo no disponible");
            return;
        }

        _hunter.TargetPoint = _target.Position;
        float distance = _hunter.DistanceTo(_target.Position);

        if (distance > _hunter.GatherDistance)
        {
            _approachTimer += Time.deltaTime;
            if (_approachTimer > MaxApproachTime)
            {
                _hunter.IgnoreBoid(_target, 8f);
                _fsm.ChangeState(_getNextState(), "No pudo llegar al Boid eliminado");
                return;
            }

            _hunter.CurrentAction = $"Yendo al Boid eliminado ({distance:0.0}m)";
            _hunter.MoveTowards(_target.Position, _hunter.GatherDistance * 0.7f);
            return;
        }

        _hunter.Stop();
        _hunter.LookAt(_target.Position);
        _gatherTimer += Time.deltaTime;
        _hunter.CurrentAction = $"Recolectando {_gatherTimer / _hunter.GatherTime * 100f:0}%";

        if (_gatherTimer >= _hunter.GatherTime)
        {
            _completed = true;
            _target.Collect();
            _hunter.OnBoidGathered(_target);
            _fsm.ChangeState(_getNextState(), "Recoleccion completada");
        }
    }

    public override void Exit()
    {
        if (!_completed && _gatherTimer > 0f && _target != null)
        {
            EventLog.Add($"{_hunter.name}: recoleccion de {_target.name} CANCELADA (no suma)", new Color(1f, 0.6f, 0.3f));
        }

        _hunter.CurrentTarget = "-";
        _hunter.TargetPoint = null;
        _target = null;
    }
}
