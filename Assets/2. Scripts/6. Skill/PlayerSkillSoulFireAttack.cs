using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSkillSoulFireAttack : MonoBehaviour
{
    private enum AimMode
    {
        Forward = 0,
        TargetFlat = 1
    }

    [Header("스킬")]
    [SerializeField] private PlayerSkillDefinitionSO skillDef;
    [SerializeField] private SkillSoulFireProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("조준")]
    [SerializeField] private AimMode aimMode = AimMode.Forward;

    [Header("발동")]
    [SerializeField] private float activationRangeOverride = 0f;

    [Header("기즈모")]
    [SerializeField] private bool drawActivationRangeGizmo = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private Color activationRangeGizmoColor = new Color(0.2f, 0.9f, 1f, 0.35f);

    [Header("발사체")]
    [SerializeField] private float projectileSpeed = 6f;
    [SerializeField] private float projectileLifeTime = 2.4f;

    private RunScope _scope;
    private PlayerSkillSystem _skills;
    private float _cooldown;

    private void Update()
    {
        Resolve();
        if (_skills == null || skillDef == null || projectilePrefab == null) return;
        if (!_skills.TryGetSkill(skillDef, out PlayerSkillSystem.SkillState state)) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f) return;

        var target = FindTarget(ResolveActivationRange(state));
        if (target == null) return;

        Fire(target, state);
        float aps = Mathf.Max(0.01f, state.AttackSpeed);
        _cooldown = 1f / aps;
    }

    private void Resolve()
    {
        if (_scope == null) _scope = RunScopeLocator.Current;
        if (_skills == null)
        {
            _skills = GetComponent<PlayerSkillSystem>();
            if (_skills == null && _scope != null)
                _skills = _scope.Entities?.Player?.Skills;
        }
    }

    private EnemyEntity FindTarget(float range)
    {
        var list = _scope?.Entities?.Enemies;
        if (list == null || list.Count == 0) return null;

        float r2 = range * range;
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

    private float ResolveActivationRange(PlayerSkillSystem.SkillState state)
    {
        if (activationRangeOverride > 0f) return activationRangeOverride;
        return state != null ? state.Range : 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected) return;
        DrawActivationRangeGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected) return;
        DrawActivationRangeGizmo();
    }
#endif

    private void DrawActivationRangeGizmo()
    {
        if (!drawActivationRangeGizmo) return;
        float range = ResolveGizmoRange();
        if (range <= 0f) return;
        Gizmos.color = activationRangeGizmoColor;
        Vector3 pos = transform.position;
        pos.y = 0f;
        Gizmos.DrawWireSphere(pos, range);
    }

    private float ResolveGizmoRange()
    {
        if (activationRangeOverride > 0f) return activationRangeOverride;
        if (Application.isPlaying && _skills != null && skillDef != null
            && _skills.TryGetSkill(skillDef, out PlayerSkillSystem.SkillState state))
        {
            return state.Range;
        }
        return skillDef != null ? skillDef.baseRange : 0f;
    }

    private void Fire(EnemyEntity target, PlayerSkillSystem.SkillState state)
    {
        Transform aimT = firePoint != null ? firePoint : transform;
        Vector3 origin = aimT.position;
        Vector3 dir = Vector3.zero;
        if (aimMode == AimMode.TargetFlat && target != null)
            dir = target.transform.position - origin;
        else
            dir = aimT.forward;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.y = 0f;
        dir.Normalize();

        float life = projectileLifeTime > 0f
            ? projectileLifeTime
            : (state.Range / Mathf.Max(0.1f, projectileSpeed));

        SkillSoulFireProjectile proj;
        if (_scope != null && _scope.App != null && _scope.App.Pool != null)
            proj = _scope.App.Pool.Spawn(projectilePrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
        else
            proj = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir, Vector3.up));

        proj.Launch(_scope, projectilePrefab.gameObject, dir, state.Damage, projectileSpeed, life);
        PlayCastSfx();
    }

    private void PlayCastSfx()
    {
        if (skillDef == null || skillDef.castSfx == null) return;
        GameAudio.Instance?.PlaySfx(skillDef.castSfx, skillDef.castSfxVolume);
    }
}
