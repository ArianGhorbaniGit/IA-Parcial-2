using UnityEngine;

//Estado Attack (lo usan los dos parciales). Al entrar elige el Boid vivo mas cercano y evalua la distancia:
// - dentro de MeleeAttackRadius: lo persigue hasta el contacto y golpea (en el Parcial 2 golpea directo)
// - dentro de RangeAttackRadius: ataque a distancia
// - fuera de rango: Pursuit hasta poder atacar
//Ataque exitoso: reinicia el TBA y vuelve a nextState. Si pierde de vista al objetivo vuelve sin reiniciar el TBA.
public class AttackState<T> : State<T>
{
    private readonly HunterBase _hunter;
    private readonly T _nextState;
    private Boid _target;

    public AttackState(FiniteStateMachine<T> fsm, HunterBase hunter, T nextState) : base(fsm, "ATTACK")
    {
        _hunter = hunter;
        _nextState = nextState;
    }

    public override void Enter()
    {
        _target = _hunter.ClosestAliveBoid;
        if (_target == null)
        {
            _fsm.ChangeState(_nextState, "No hay Boid para atacar");
            return;
        }

        _hunter.CurrentTarget = _target.name;
        _hunter.CurrentAction = "Evaluando distancia";
        EventLog.Add($"{_hunter.name} elige objetivo: {_target.name} ({_hunter.DistanceTo(_target.Position):0.0}m)", new Color(1f, 0.5f, 0.5f));
    }

    public override void Update()
    {
        if (_target == null || !_target.IsAlive || !_hunter.IsInVision(_target))
        {
            _fsm.ChangeState(_nextState, "El objetivo salio del rango de vision (no se reinicia el TBA)");
            return;
        }

        _hunter.TargetPoint = _target.Position;
        float distance = _hunter.DistanceTo(_target.Position);

        if (distance <= _hunter.MeleeAttackRadius)
        {
            if (distance <= _hunter.MeleeHitDistance)
            {
                _hunter.Stop();
                _hunter.MeleeAttack(_target);
                _fsm.ChangeState(_nextState, "Ataque cuerpo a cuerpo exitoso: se reinicia el TBA");
                return;
            }

            _hunter.CurrentAction = $"Persiguiendo para golpear ({distance:0.0}m)";
            _hunter.Pursue(_target);
        }
        else if (distance <= _hunter.RangeAttackRadius)
        {
            _hunter.Stop();
            _hunter.RangedAttack(_target);
            _fsm.ChangeState(_nextState, "Ataque a distancia exitoso: se reinicia el TBA");
        }
        else
        {
            _hunter.CurrentAction = $"Pursuit, fuera de rango ({distance:0.0}m)";
            _hunter.Pursue(_target);
        }
    }

    public override void Exit()
    {
        _hunter.CurrentTarget = "-";
        _hunter.TargetPoint = null;
        _target = null;
    }
}
