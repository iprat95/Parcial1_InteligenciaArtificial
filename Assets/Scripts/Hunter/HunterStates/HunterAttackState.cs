using UnityEngine;

public class HunterAttackState : State
{
    private readonly IHunterContext _agent;

    public HunterAttackState(IHunterContext agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _agent.SetLastAction("Entrando en Attack");
    }

    public override void Update()
    {
        var target = _agent.CurrentTarget;

        if (target == null || target.IsEliminated)
        {
            StateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        float distance = Vector3.Distance(_agent.Transform.position, target.transform.position);
        
        if (distance > _agent.VisionRadius)
        {
            StateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        if (distance <= _agent.MeleeAttackRadius)
        {
            _agent.PerformMeleeAttack(target);
            StateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        if (distance <= _agent.RangeAttackRadius)
        {
            _agent.PerformRangeAttack(target);
            StateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        _agent.MoveTowards(target.transform.position);
        _agent.SetLastAction($"Persiguiendo a {target.name}");
    }
}
