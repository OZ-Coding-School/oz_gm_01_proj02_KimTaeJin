using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float _turnSpeed = 720f;

    [Header("Boundary (optional)")]
    [SerializeField] private PlayAreaBoundary boundary;
    [SerializeField] private bool clampToBoundary = true;
    [SerializeField] private float boundaryRadius = -1f;

    private float _baseMoveSpeed;  
    private float _moveSpeedMul = 1f; 

    private Rigidbody _rb;
    private Animator _anim;
    private bool _boundaryResolved;
    private bool _boundaryRadiusResolved;
    private float _cachedBoundaryRadius;

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

        ResolveBoundary();
        CacheBoundaryRadius();
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
            if (clampToBoundary && ResolveBoundary())
                next = boundary.ClampInsideXZ(next, _cachedBoundaryRadius);
            _rb.MovePosition(next);
        }
        else
        {
            Vector3 next = transform.position + delta;
            if (clampToBoundary && ResolveBoundary())
                next = boundary.ClampInsideXZ(next, _cachedBoundaryRadius);
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
    }

    private bool ResolveBoundary()
    {
        if (boundary != null) return true;
        if (_boundaryResolved) return false;
        _boundaryResolved = true;
        boundary = FindObjectOfType<PlayAreaBoundary>();
        return boundary != null;
    }

    private void CacheBoundaryRadius()
    {
        if (_boundaryRadiusResolved) return;
        _boundaryRadiusResolved = true;

        float radius = boundaryRadius;
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

        _cachedBoundaryRadius = Mathf.Max(0f, radius);
    }

}
