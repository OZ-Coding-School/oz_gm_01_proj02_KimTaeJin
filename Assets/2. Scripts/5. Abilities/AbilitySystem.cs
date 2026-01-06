using UnityEngine;

public sealed class AbilitySystem : MonoBehaviour
{
    private RunScope _scope;
    private float _cd;

    public void Construct(RunScope scope) => _scope = scope;

    private void Update()
    {
        if (_scope == null) return;

        _cd -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (_cd > 0f) return;

            CastAoE();
            _cd = 2f; // 하드코딩 쿨타임
        }
    }

    private void CastAoE()
    {
        var p = _scope.Entities.Player.transform.position;
        float r = 3f;
        float r2 = r * r;

        for (int i = _scope.Entities.Enemies.Count - 1; i >= 0; i--)
        {
            var e = _scope.Entities.Enemies[i];
            if (e == null) continue;

            var d = e.transform.position - p;
            d.y = 0f;

            if (d.sqrMagnitude <= r2)
                _scope.Combat.DealDamage(e, 10); // 하드코딩 데미지
        }

        Debug.Log("[Ability] AoE Cast");
    }
}
