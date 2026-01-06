using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    private float _moveSpeed = 6f;
    private float _turnSpeed = 720f; 

    private Rigidbody _rb;
    private Animator _anim;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");

    public void Initialize(float speed)
    {
        _moveSpeed = speed;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _anim = GetComponentInChildren<Animator>();
    }

    private void FixedUpdate()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(x, 0f, z);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        Vector3 delta = inputDir * (_moveSpeed * Time.fixedDeltaTime);

        if (_rb != null)
            _rb.MovePosition(_rb.position + delta);
        else
            transform.position += delta;

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
            _anim.SetFloat(MoveSpeedHash, inputDir.magnitude);
    }
}
