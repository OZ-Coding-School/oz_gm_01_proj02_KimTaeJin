using UnityEngine;
using UnityEngine.Serialization;

public sealed class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float _turnSpeed = 720f;

    [Header("Collision Fix")]
    [SerializeField] private bool lockExternalPush = true;

    [Header("Boundary (soft radius)")]
    [SerializeField] private PlayAreaProgressController playArea;
    [SerializeField] private bool clampToRadius = true;
    [SerializeField] private float softPushSpeed = 6f;
    [SerializeField] private bool cancelOutwardMove = true;
    [FormerlySerializedAs("boundaryRadius")]
    [SerializeField] private float playerRadiusOverride = -1f;

    private float _baseMoveSpeed;  
    private float _moveSpeedMul = 1f; 

    private Rigidbody _rb;
    private Animator _anim;
    private bool _playAreaResolved;
    private bool _playerRadiusResolved;
    private float _cachedPlayerRadius;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    public float CurrentMoveSpeed => _baseMoveSpeed * _moveSpeedMul;
    public void SetBaseMoveSpeed(float baseSpeed) => _baseMoveSpeed = Mathf.Max(0f, baseSpeed);
    public void SetMoveSpeedMultiplier(float mul) => _moveSpeedMul = Mathf.Max(0f, mul);
    public void AddMoveSpeedMultiplier(float add) => _moveSpeedMul = Mathf.Max(0f, _moveSpeedMul + add);

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null) _anim.applyRootMotion = false;

        if (_rb != null)
        {
            _rb.useGravity = false;
            _rb.isKinematic = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        ResolvePlayArea();
        CachePlayerRadius();
    }

    private void FixedUpdate()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(x, 0f, z);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        Vector3 delta = inputDir * (CurrentMoveSpeed * Time.fixedDeltaTime);

        if (_rb != null)
        {
            Vector3 next = _rb.position + delta;
            next.y = _rb.position.y;
            if (clampToRadius && ResolvePlayArea())
                next = ApplySoftRadiusClamp(_rb.position, next);
            _rb.MovePosition(next);
        }
        else
        {
            Vector3 next = transform.position + delta;
            if (clampToRadius && ResolvePlayArea())
                next = ApplySoftRadiusClamp(transform.position, next);
            transform.position = next;
        }

        if (inputDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(inputDir, Vector3.up);

            if (_rb != null)
            {
                Quaternion newRot = Quaternion.RotateTowards(_rb.rotation, targetRot, _turnSpeed * Time.fixedDeltaTime);
                _rb.MoveRotation(newRot);
            }
            else
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, _turnSpeed * Time.fixedDeltaTime);
            }
        }

        if (_anim != null)
        {
            float animMove = inputDir.magnitude * _moveSpeedMul;
            _anim.SetFloat(MoveSpeedHash, animMove);
        }

        if (_rb != null && lockExternalPush)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    private bool ResolvePlayArea()
    {
        if (playArea != null) return true;
        if (_playAreaResolved) return false;
        _playAreaResolved = true;
        playArea = FindObjectOfType<PlayAreaProgressController>();
        return playArea != null;
    }

    private void CachePlayerRadius()
    {
        if (_playerRadiusResolved) return;
        _playerRadiusResolved = true;

        float radius = playerRadiusOverride;
        if (radius <= 0f)
        {
            radius = 0.35f;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
                radius = capsule.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
            else
            {
                var sphere = GetComponent<SphereCollider>();
                if (sphere != null)
                    radius = sphere.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
                else
                {
                    var controller = GetComponent<CharacterController>();
                    if (controller != null)
                        radius = controller.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
                }
            }
        }

        _cachedPlayerRadius = Mathf.Max(0f, radius);
    }

    private Vector3 ApplySoftRadiusClamp(Vector3 current, Vector3 desired)
    {
        float padding = _cachedPlayerRadius;
        if (playArea.IsInsideXZ(desired, padding))
            return desired;

        Vector3 clamped = playArea.ClampPointXZ(desired, padding);
        Vector3 outVec = desired - clamped;
        outVec.y = 0f;
        float outMag = outVec.magnitude;
        if (outMag <= 0.0001f)
            return desired;

        Vector3 dir = outVec / outMag;

        if (cancelOutwardMove)
        {
            Vector3 delta = desired - current;
            delta.y = 0f;
            float outward = Vector3.Dot(delta, dir);
            if (outward > 0f)
                desired -= dir * outward;
        }

        float push = Mathf.Min(outMag, Mathf.Max(0.01f, softPushSpeed) * Time.fixedDeltaTime);
        desired -= dir * push;
        desired.y = current.y;
        return desired;
    }

}
