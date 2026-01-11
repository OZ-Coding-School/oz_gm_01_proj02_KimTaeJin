using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float _turnSpeed = 720f;

    private float _baseMoveSpeed;  
    private float _moveSpeedMul = 1f; 

    private Rigidbody _rb;
    private Animator _anim;

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
            _rb.MovePosition(next);
        }
        else
        {
            transform.position += delta;
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

}
