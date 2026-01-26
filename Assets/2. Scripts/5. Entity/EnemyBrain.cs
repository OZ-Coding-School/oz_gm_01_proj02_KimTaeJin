using UnityEngine;

public sealed class EnemyBrain : MonoBehaviour
{
    public enum TargetMode
    {
        TowerOrCenter = 0,
        Center = 1,
        Player = 2
    }

    [Header("Refs")]
    [SerializeField] private Transform visual;
    [SerializeField] private Animator animator;

    [Header("Tuning")]
    [SerializeField] private float rotateLerp = 12f;
    [SerializeField] private float moveThreshold = 0.02f;
    [SerializeField] private TargetMode targetMode = TargetMode.TowerOrCenter;
    [SerializeField] private float retargetInterval = 0.3f;

    [Header("Attack (Building)")]
    [SerializeField] private float attackRange = 1.3f;
    [SerializeField] private float attacksPerSecond = 1f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private bool useAttackAnimation = true;
    [SerializeField] private bool drawAttackRangeGizmo = true;
    [SerializeField] private Color attackRangeGizmoColor = new Color(0.35f, 1f, 0.35f, 0.4f);
    [SerializeField] private float attackRangeGizmoYOffset = 0f;

    [Header("Player Priority")]
    [SerializeField] private bool usePlayerPriority = true;
    [SerializeField] private float playerPriorityRange = 5f;
    [SerializeField] private float playerPriorityLoseRange = 7f;
    [SerializeField] private bool drawPlayerPriorityGizmo = true;
    [SerializeField] private Color playerPriorityGizmoColor = new Color(1f, 0.85f, 0.2f, 0.45f);
    [SerializeField] private Color playerPriorityLoseGizmoColor = new Color(1f, 0.45f, 0.1f, 0.25f);

    private RunScope _scope;
    private float _baseSpeed;
    private float _speedMultiplier = 1f;
    private Rigidbody _rb;
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private float _knockTimer;
    private Vector3 _knockVel;
    private Transform _currentTarget;
    private float _retargetTimer;
    private bool _playerPriorityActive;
    private float _attackTimer;

    public void Construct(RunScope scope, float speed)
    {
        _scope = scope;
        _baseSpeed = speed;

        if (visual == null) visual = transform;
        if (animator == null) animator = visual.GetComponentInChildren<Animator>();

        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();

        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.isKinematic = false;

        _rb.drag = 8f;
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void Knockback(Vector3 dir, float force, float duration = 0.12f)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        _knockTimer = Mathf.Max(_knockTimer, duration);
        _knockVel = dir * (force / duration);

        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (_scope == null) return;

        if (_knockTimer > 0f)
        {
            _knockTimer -= Time.fixedDeltaTime;
            _rb.MovePosition(_rb.position + _knockVel * Time.fixedDeltaTime);

            if (_knockTimer <= 0f)
                _rb.velocity = Vector3.zero;

            return;
        }

        _rb.velocity = Vector3.zero;
        UpdateTarget();

        if (_currentTarget == null) return;

        Vector3 toTarget = _currentTarget.position - _rb.position;
        toTarget.y = 0f;

        float distSqr = toTarget.sqrMagnitude;
        bool moving = distSqr > (moveThreshold * moveThreshold);
        bool attacking = TryAttackBuilding(_currentTarget, distSqr);

        Vector3 dir = moving && !attacking ? toTarget.normalized : Vector3.zero;

        if (moving && !attacking)
        {
            float moveSpeed = _baseSpeed * Mathf.Max(0f, _speedMultiplier);
            Vector3 next = _rb.position + dir * (moveSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
        }

        if (moving && !attacking && visual != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, targetRot, rotateLerp * Time.fixedDeltaTime);
        }

        if (animator != null)
            animator.SetFloat(MoveSpeedHash, moving && !attacking ? 1f : 0f, 0.08f, Time.deltaTime);
    }

    private void UpdateTarget()
    {
        _retargetTimer -= Time.fixedDeltaTime;
        if (_currentTarget != null && _retargetTimer > 0f) return;

        _currentTarget = ResolveTarget();
        _retargetTimer = Mathf.Max(0.05f, retargetInterval);
    }

    private Transform ResolveTarget()
    {
        var player = _scope.Entities?.Player;
        if (targetMode == TargetMode.Player)
            return player != null ? player.transform : null;

        if (targetMode == TargetMode.TowerOrCenter && usePlayerPriority && player != null
            && ShouldPrioritizePlayer(player.transform))
            return player.transform;

        switch (targetMode)
        {
            case TargetMode.Center:
                return ResolveCenterTarget();
            default:
                return ResolveTowerOrCenter();
        }
    }

    private bool ShouldPrioritizePlayer(Transform player)
    {
        if (player == null)
        {
            _playerPriorityActive = false;
            return false;
        }

        float enter = Mathf.Max(0.1f, playerPriorityRange);
        float exit = Mathf.Max(enter, playerPriorityLoseRange);
        Vector3 origin = _rb != null ? _rb.position : transform.position;
        Vector3 delta = player.position - origin;
        delta.y = 0f;
        float distSqr = delta.sqrMagnitude;

        if (_playerPriorityActive)
        {
            if (distSqr > exit * exit)
                _playerPriorityActive = false;
        }
        else
        {
            if (distSqr <= enter * enter)
                _playerPriorityActive = true;
        }

        return _playerPriorityActive;
    }

    private Transform ResolveTowerOrCenter()
    {
        Transform best = ResolveSharedBuildingTarget();
        if (best != null) return best;
        return ResolveCenterTarget();
    }

    private Transform ResolveSharedBuildingTarget()
    {
        var list = SharedBuildingHealthProxy.Active;
        if (list == null || list.Count == 0) return null;

        Transform best = null;
        float bestD = float.MaxValue;
        Vector3 p = _rb.position;

        for (int i = 0; i < list.Count; i++)
        {
            var proxy = list[i];
            if (proxy == null || !proxy.isActiveAndEnabled) continue;
            var shared = proxy.SharedHealth;
            if (shared == null || shared.IsDead) continue;

            Transform t = proxy.transform;
            if (t == null || t == transform) continue;
            Vector3 d = t.position - p;
            d.y = 0f;
            float dd = d.sqrMagnitude;
            if (dd < bestD)
            {
                bestD = dd;
                best = t;
            }
        }

        return best;
    }

    private Transform ResolveCenterTarget()
    {
        if (_scope.Grid != null && _scope.Grid.Anchor != null)
            return _scope.Grid.Anchor;

        var player = _scope.Entities?.Player;
        return player != null ? player.transform : null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        if (drawAttackRangeGizmo)
        {
            float range = Mathf.Max(0.05f, attackRange);
            Gizmos.color = attackRangeGizmoColor;
            Vector3 center = visual != null ? visual.position : pos;
            center.y += attackRangeGizmoYOffset;
            Gizmos.DrawWireSphere(center, range);
        }

        if (!drawPlayerPriorityGizmo) return;

        float enter = Mathf.Max(0.1f, playerPriorityRange);
        float exit = Mathf.Max(enter, playerPriorityLoseRange);

        Gizmos.color = playerPriorityGizmoColor;
        Gizmos.DrawWireSphere(pos, enter);

        if (exit > enter + 0.01f)
        {
            Gizmos.color = playerPriorityLoseGizmoColor;
            Gizmos.DrawWireSphere(pos, exit);
        }
    }

    private bool TryAttackBuilding(Transform target, float distSqr)
    {
        if (target == null) return false;
        var proxy = target.GetComponentInParent<SharedBuildingHealthProxy>();
        if (proxy == null) return false;
        if (proxy.SharedHealth == null || proxy.SharedHealth.IsDead) return false;

        float range = Mathf.Max(0.05f, attackRange);
        if (distSqr > range * range) return false;

        _attackTimer -= Time.fixedDeltaTime;
        if (_attackTimer > 0f) return true;

        _attackTimer = attacksPerSecond > 0f ? (1f / attacksPerSecond) : 1f;
        if (useAttackAnimation && animator != null)
            animator.SetTrigger(AttackHash);

        proxy.ApplyDamage(Mathf.Max(0, attackDamage));
        return true;
    }
}
