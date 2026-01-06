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
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    public void Construct(RunScope scope, float speed)
    {
        _scope = scope;
        _speed = speed;

        if (visual == null) visual = transform;

        if (animator == null)
            animator = visual.GetComponentInChildren<Animator>();

        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();

        _rb.useGravity = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.isKinematic = false; 
    }

    private void FixedUpdate()
    {
        var player = _scope?.Entities?.Player;
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
        {
            float moveSpeed01 = moving ? 1f : 0f; 
            animator.SetFloat(MoveSpeedHash, moveSpeed01);

            animator.SetFloat(MoveXHash, dir.x);
            animator.SetFloat(MoveZHash, dir.z);

            animator.SetBool(IsMovingHash, moving);
        }
    }
}
