using UnityEngine;

[DisallowMultipleComponent]
public sealed class TowerEntity : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] private bool rotateToTarget = true;

    [Header("Pivot (optional)")]
    [SerializeField] private Transform yawPivot;   
    [SerializeField] private Transform muzzle;     

    private RunScope _scope;
    private TowerDefinitionSO _def;
    private float _cool;
    private bool _constructed;

    public Vector2Int Cell { get; private set; }

    public void SetCell(Vector2Int cell) => Cell = cell;

    public void Construct(RunScope scope, TowerDefinitionSO def)
    {
        _scope = scope;
        _def = def;
        _cool = Random.Range(0f, Mathf.Max(0.02f, def.fireInterval));
        _constructed = true;
    }

    private void Update()
    {
        if (!_constructed || _scope == null || _def == null) return;

        _cool -= Time.deltaTime;
        if (_cool > 0f) return;

        var target = FindTarget();
        if (target == null)
        {
            _cool = 0.05f;
            return;
        }

        if (rotateToTarget)
        {
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;

            if (to.sqrMagnitude > 0.0001f)
            {
                Transform rotT = (yawPivot != null) ? yawPivot : transform;
                rotT.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
        }

        Fire(target);
        _cool = Mathf.Max(0.02f, _def.fireInterval);
    }

    private EnemyEntity FindTarget()
    {
        var list = _scope.Entities.Enemies;
        if (list == null || list.Count == 0) return null;

        float r2 = _def.range * _def.range;
        EnemyEntity best = null;
        float bestD = float.MaxValue;

        Vector3 p = transform.position;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e == null || !e.gameObject.activeInHierarchy) continue;

            Vector3 d = e.transform.position - p;
            d.y = 0f;
            float dd = d.sqrMagnitude;

            if (dd <= r2 && dd < bestD)
            {
                bestD = dd;
                best = e;
            }
        }

        return best;
    }

    private void Fire(EnemyEntity target)
    {
        if (_def.projectilePrefab != null && _scope?.App?.Pool != null)
        {
            Transform rotT = (yawPivot != null) ? yawPivot : transform;
            Vector3 spawn = (muzzle != null)
                ? muzzle.position
                : (rotT.position + rotT.forward * 0.6f + Vector3.up * 0.2f);

            Vector3 dir = target.transform.position - spawn;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = rotT.forward;
            dir.Normalize();

            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            var proj = _scope.App.Pool.Spawn(_def.projectilePrefab, spawn, rot);

            proj.Launch(
                _scope,
                _def.projectilePrefab.gameObject,
                dir,
                _def.damage,
                _def.projectileSpeed,
                _def.projectileLifeTime,
                _def.knockback
            );

            return;
        }

        _scope.Combat.DealDamage(target, _def.damage);

        if (_def.knockback > 0f)
            _scope.Combat.Knockback(target, transform.position, _def.knockback);
    }

    private void OnDestroy()
    {
        if (_scope != null)
        {
            _scope.Entities?.UnregisterTower(this);
            _scope.Grid?.Release(Cell);
        }
    }
}
