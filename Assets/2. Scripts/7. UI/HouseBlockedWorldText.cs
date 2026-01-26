using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class HouseBlockedWorldText : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HouseDrift houseDrift;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private GameObject messageRoot;
    [SerializeField] private Transform followRoot;
    [SerializeField] private Camera targetCamera;

    [Header("Message")]
    [SerializeField] private string blockedMessage = "길이 막혔습니다!";

    [Header("Follow")]
    [SerializeField] private bool useBlockedSelfPoint = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private bool useSmoothFollow = true;
    [SerializeField] private float followSpeed = 12f;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool lockToCameraForward = true;

    [Header("Render")]
    [SerializeField] private bool ignoreDepth = true;

    private bool _visible;
    private Material _runtimeMaterial;

    private void Awake()
    {
        if (followRoot == null) followRoot = transform;
        if (messageText == null) messageText = GetComponentInChildren<TMP_Text>(true);
        if (messageRoot == null && messageText != null)
            messageRoot = messageText.gameObject;
        if (houseDrift == null) houseDrift = FindObjectOfType<HouseDrift>();
        if (messageText != null) messageText.text = blockedMessage;
        SetupDepth();
        ApplyVisible(false);
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }

    private void LateUpdate()
    {
        if (houseDrift == null || messageText == null || followRoot == null) return;

        if (!houseDrift.IsBlocked)
        {
            if (_visible) ApplyVisible(false);
            return;
        }

        if (!_visible) ApplyVisible(true);

        Vector3 blockedPoint = useBlockedSelfPoint ? houseDrift.BlockedSelfPoint : houseDrift.BlockedPoint;
        Vector3 target = blockedPoint + worldOffset;
        if (useSmoothFollow)
            followRoot.position = Vector3.MoveTowards(followRoot.position, target, followSpeed * Time.deltaTime);
        else
            followRoot.position = target;

        if (faceCamera)
            AlignToCamera();
    }

    private void ApplyVisible(bool on)
    {
        _visible = on;
        if (messageRoot != null && messageRoot != gameObject)
        {
            messageRoot.SetActive(on);
            return;
        }

        if (messageText != null)
            messageText.enabled = on;
    }

    private void SetupDepth()
    {
        if (!ignoreDepth || messageText == null) return;
        _runtimeMaterial = new Material(messageText.fontSharedMaterial);
        _runtimeMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
        messageText.fontMaterial = _runtimeMaterial;
    }

    private void AlignToCamera()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;
        if (lockToCameraForward)
        {
            Vector3 forward = cam.transform.forward;
            if (forward.sqrMagnitude < 0.0001f) return;
            followRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);
            return;
        }

        Vector3 to = followRoot.position - cam.transform.position;
        if (to.sqrMagnitude < 0.0001f) return;
        followRoot.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }
}


