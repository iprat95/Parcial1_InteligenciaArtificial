using UnityEngine;

public class HunterGatherState : State
{
    private readonly IHunterContext _agent;
    private float _timer;

    public HunterGatherState(IHunterContext agent, StateMachine stateMachine) : base(stateMachine)
    {
        _agent = agent;
    }

    public override void Enter()
    {
        _timer = 0f;
        _agent.SetLastAction("Yendo a recolectar");
    }

    public override void Update()
    {
        var target = _agent.CurrentTarget;

        if (target == null || !target.IsEliminated)
        {
            StateMachine.ChangeState(HunterStates.Patrol);
            return;
        }

        float distance = Vector3.Distance(_agent.Transform.position, target.transform.position);

        if (distance > _agent.GatherInteractDistance)
        {
            _agent.MoveTowards(target.transform.position);
            _timer = 0f;
            return;
        }

        _timer += Time.deltaTime;
        _agent.SetLastAction($"Recolectando a {target.name} ({_timer:0.0}/{_agent.GatherDuration:0.0}s)");

        if (_timer >= _agent.GatherDuration)
        {
            target.OnGathered();
            StateMachine.ChangeState(HunterStates.Patrol);
        }
    }
}
