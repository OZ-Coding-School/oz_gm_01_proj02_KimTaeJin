using UnityEngine;

public sealed class EnemyBrain : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform visual;
    [SerializeField] private Animator animator;

    [Header("Tuning")]
    [SerializeField] private float rotateLerp = 12f;
    [SerializeField] private float moveThreshold = 0.02f;

    private RunScope _scope;
    private float _speed;
    private Rigidbody _rb;
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");

    private float _knockTimer;
    private Vector3 _knockVel;

    public void Construct(RunScope scope, float speed)
    {
        _scope = scope;
        _speed = speed;

        if (visual == null) visual = transform;
        if (animator == null) animator = visual.GetComponentInChildren<Animator>();

        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();

        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.isKinematic = false;

        _rb.drag = 8f;
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

        var player = _scope.Entities?.Player;
        if (player == null) return;

        Vector3 toPlayer = player.transform.position - _rb.position;
        toPlayer.y = 0f;

        float distSqr = toPlayer.sqrMagnitude;
        bool moving = distSqr > (moveThreshold * moveThreshold);

        Vector3 dir = moving ? toPlayer.normalized : Vector3.zero;

        if (moving)
        {
            Vector3 next = _rb.position + dir * (_speed * Time.fixedDeltaTime);
            _rb.MovePosition(next);
        }

        if (moving && visual != null)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, targetRot, rotateLerp * Time.fixedDeltaTime);
        }

        if (animator != null)
            animator.SetFloat(MoveSpeedHash, moving ? 1f : 0f, 0.08f, Time.deltaTime);
    }
}
