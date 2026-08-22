using System;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class TowerEntity : MonoBehaviour
{
    [Header("Optional")]
    [SerializeField] private bool rotateToTarget = true;

    [Header("Pivot (optional)")]
    [SerializeField] private Transform yawPivot;
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private Transform muzzle;
    [SerializeField] private bool invertPitch = false;

    private RunScope _scope;
    private TowerDefinitionSO _def;
    private float _cool;
    private bool _constructed;
    private Vector2Int[] _occupiedCells;
    private bool _pivotsResolved;
    private bool _pitchBaseCached;
    private Quaternion _pitchBaseLocalRot = Quaternion.identity;
    private bool _suppressGridRelease;

    public Vector2Int Cell { get; private set; }
    public Vector2Int OffsetFromCenter { get; private set; }
    public TowerDefinitionSO Definition => _def;
    public Vector2Int Footprint { get; private set; } = Vector2Int.one;

    public void SetCell(Vector2Int cell) => Cell = cell;
    public void SetOffsetFromCenter(Vector2Int offset) => OffsetFromCenter = offset;
    public void SetFootprint(Vector2Int footprint)
    {
        Footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
    }

    public void SuppressGridRelease()
    {
        _suppressGridRelease = true;
    }

    public void SetOccupiedCells(System.Collections.Generic.List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            _occupiedCells = null;
            return;
        }
        _occupiedCells = cells.ToArray();
    }

    public void Construct(RunScope scope, TowerDefinitionSO def)
    {
        _scope = scope;
        _def = def;
        var passive = GetComponent<TowerPassiveBuff>();
        if (passive == null && def != null)
        {
            if (def.passiveExpGainBonus > 0f || def.passiveTowerDamageBonus > 0f || def.passiveTowerAttackSpeedBonus > 0f)
                passive = gameObject.AddComponent<TowerPassiveBuff>();
        }
        if (passive != null) passive.Configure(def);
        _cool = Random.Range(0f, GetFireInterval());
        _constructed = true;
    }

    private void Awake()
    {
        ResolvePivots();
    }

    private void Update()
    {
        if (!_constructed || _scope == null || _def == null) return;

        if (_def.attackSpeed <= 0f) return;

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
            ResolvePivots();
            Transform yawT = (yawPivot != null) ? yawPivot : transform;
            Vector3 to = target.transform.position - yawT.position;
            Vector3 toYaw = to;
            toYaw.y = 0f;

            if (toYaw.sqrMagnitude > 0.0001f)
            {
                yawT.rotation = Quaternion.LookRotation(toYaw.normalized, Vector3.up);
            }

            if (pitchPivot != null)
            {
                Vector3 toPitch = target.transform.position - pitchPivot.position;
                Vector3 local = yawT.InverseTransformDirection(toPitch);
                float forward = new Vector2(local.x, local.z).magnitude;
                if (forward < 0.0001f) forward = 0.0001f;
                float pitch = Mathf.Atan2(local.y, forward) * Mathf.Rad2Deg;
                float signedPitch = invertPitch ? pitch : -pitch;
                pitchPivot.localRotation = _pitchBaseLocalRot * Quaternion.Euler(signedPitch, 0f, 0f);
            }
        }

        Fire(target);
        _cool = GetFireInterval();
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
        float damageMul = _scope != null ? _scope.TowerDamageMultiplier : 1f;
        int dmg = Mathf.Max(0, Mathf.RoundToInt(_def.damage * damageMul));
        if (_def.projectilePrefab != null && _scope?.App?.Pool != null)
        {
            ResolvePivots();
            Transform yawT = (yawPivot != null) ? yawPivot : transform;
            Transform aimT = (pitchPivot != null) ? pitchPivot : yawT;
            Vector3 spawn = (muzzle != null)
                ? muzzle.position
                : (aimT.position + aimT.forward * 0.6f + Vector3.up * 0.2f);

            Vector3 dir = target.transform.position - spawn;
            if (pitchPivot == null) dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = aimT.forward;
            dir.Normalize();

            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            var proj = _scope.App.Pool.Spawn(_def.projectilePrefab, spawn, rot);

            proj.Launch(
                _scope,
                _def.projectilePrefab.gameObject,
                dir,
                dmg,
                _def.projectileSpeed,
                _def.projectileLifeTime,
                _def.knockback,
                _def.hitEffect
            );

            PlayFireSfx();
            return;
        }

        _scope.Combat.DealDamage(target, dmg);

        if (_def.knockback > 0f)
            _scope.Combat.Knockback(target, transform.position, _def.knockback);

        PlayFireSfx();
    }

    private void PlayFireSfx()
    {
        if (_def == null || _def.fireSfx == null) return;
        GameAudio.Instance?.PlaySfx(_def.fireSfx, _def.fireSfxVolume);
    }

    private void OnDestroy()
    {
        if (_scope != null)
        {
            _scope.Entities?.UnregisterTower(this);
            if (!_suppressGridRelease)
            {
                GridDataService dataService = _scope.GridData != null ? _scope.GridData : RunScopeLocator.Current?.GridData;
                if (dataService != null)
                {
                    Vector3Int cell3 = new Vector3Int(Cell.x, 0, Cell.y);
                    if (dataService.TryGet(cell3, out GridDataService.TowerData data))
                    {
                        if (_def != null && string.Equals(data.towerId, _def.id, StringComparison.Ordinal))
                            dataService.TryRemove(cell3);
                    }
                }
                else if (_scope.Grid != null)
                {
                    if (_occupiedCells != null && _occupiedCells.Length > 0)
                    {
                        _scope.Grid.ReleaseAll(_occupiedCells);
                    }
                    else
                    {
                        for (int y = 0; y < Footprint.y; y++)
                        {
                            for (int x = 0; x < Footprint.x; x++)
                                _scope.Grid.Release(new Vector2Int(Cell.x + x, Cell.y + y));
                        }
                    }
                }
            }
        }
    }

    private void ResolvePivots()
    {
        if (_pivotsResolved) return;
        _pivotsResolved = true;

        Transform baseT = (yawPivot != null) ? yawPivot : transform;
        if (pitchPivot == null)
        {
            pitchPivot = baseT.Find("PitchPivot");
            if (pitchPivot == null)
                pitchPivot = baseT.Find("PitchPivotOrigin");
            if (pitchPivot == null)
                pitchPivot = baseT.Find("Pitch");
        }

        if (pitchPivot != null && !_pitchBaseCached)
        {
            _pitchBaseLocalRot = pitchPivot.localRotation;
            _pitchBaseCached = true;
        }
    }

    public bool TryGetAimSnapshot(out Quaternion yawWorldRot, out Quaternion pitchLocalRot, out bool hasPitch)
    {
        ResolvePivots();
        Transform yawT = yawPivot != null ? yawPivot : transform;
        yawWorldRot = yawT.rotation;
        if (pitchPivot != null)
        {
            pitchLocalRot = pitchPivot.localRotation;
            hasPitch = true;
        }
        else
        {
            pitchLocalRot = Quaternion.identity;
            hasPitch = false;
        }
        return true;
    }

    public void ApplyAimSnapshot(Quaternion yawWorldRot, Quaternion pitchLocalRot, bool hasPitch)
    {
        ResolvePivots();
        Transform yawT = yawPivot != null ? yawPivot : transform;
        yawT.rotation = yawWorldRot;
        if (hasPitch && pitchPivot != null)
            pitchPivot.localRotation = pitchLocalRot;
    }

    private float GetFireInterval()
    {
        float aps = _def != null ? _def.attackSpeed : 1f;
        if (aps <= 0f) return 9999f;
        float mul = (_scope != null) ? _scope.TowerAttackSpeedMultiplier : 1f;
        float finalAps = Mathf.Max(0.01f, aps * Mathf.Max(0.01f, mul));
        return 1f / finalAps;
    }
}
