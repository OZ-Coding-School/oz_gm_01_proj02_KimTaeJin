using UnityEngine;

public sealed class PlayerEntity : MonoBehaviour
{
    private RunScope _scope;

    public void Construct(RunScope scope)
    {
        _scope = scope;

        var controller = GetComponent<PlayerController>();
        if (controller == null) controller = gameObject.AddComponent<PlayerController>();
        controller.Initialize(speed: 6f);

        var ability = GetComponent<AbilitySystem>();
        if (ability == null) ability = gameObject.AddComponent<AbilitySystem>();
        ability.Construct(_scope);

        var melee = GetComponent<PlayerMeleeAutoAttack>();
        if (melee == null) melee = gameObject.AddComponent<PlayerMeleeAutoAttack>();
        melee.Construct(_scope);

    }
}
