using UnityEngine;
using Cinemachine;

[DisallowMultipleComponent]
public sealed class PlayAreaCameraClamp : CinemachineExtension
{
    public enum PaddingAxisMode
    {
        Both = 0,
        XOnly = 1,
        ZOnly = 2
    }

    [SerializeField] private PlayAreaProgressController playArea;
    [SerializeField] private bool autoFindPlayArea = true;
    [SerializeField] private bool autoPadding = true;
    [SerializeField] private float extraPadding = 0f;
    [SerializeField] private PaddingAxisMode paddingAxis = PaddingAxisMode.Both;
    [SerializeField] private float planeYOffset = 0f;
    [SerializeField] private float minAutoPadding = 0f;
    [SerializeField] private float maxAutoPadding = 100f;
    [SerializeField] private float fallbackPadding = 4f;
    [Header("Gizmo")]
    [SerializeField] private bool drawClampGizmo = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private int clampGizmoSegments = 48;
    [SerializeField] private float clampGizmoYOffset = 0.06f;
    [SerializeField] private Color clampGizmoColor = new Color(0.2f, 0.75f, 1f, 0.9f);

    private bool _playAreaResolved;
    private Vector2 _lastPadding;
    private bool _hasLastPadding;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;
        if (!ResolvePlayArea()) return;

        Vector3 pos = state.FinalPosition;
        float padding = extraPadding;
        if (autoPadding)
            padding += ComputeAutoPadding(ref state);
        Vector2 paddingVec = ToPaddingVector(padding);
        _lastPadding = paddingVec;
        _hasLastPadding = true;
        Vector3 clamped = playArea.ClampPointXZ(pos, paddingVec);
        Vector3 delta = clamped - pos;
        if (delta.sqrMagnitude > 0.0000001f)
            state.PositionCorrection += delta;
    }

    private bool ResolvePlayArea()
    {
        if (playArea != null) return true;
        if (!autoFindPlayArea || _playAreaResolved) return false;
        _playAreaResolved = true;
        playArea = FindObjectOfType<PlayAreaProgressController>();
        return playArea != null;
    }

    private float ComputeAutoPadding(ref CameraState state)
    {
        float planeY = playArea.Center.y + planeYOffset;
        Vector3 camPos = state.FinalPosition;
        Quaternion camRot = state.FinalOrientation;
        Vector3 forward = camRot * Vector3.forward;

        float padding = fallbackPadding;

        if (state.Lens.Orthographic)
        {
            float halfH = Mathf.Max(0.001f, state.Lens.OrthographicSize);
            float halfW = halfH * Mathf.Max(0.01f, state.Lens.Aspect);
            Vector3 right = camRot * Vector3.right;
            Vector3 up = camRot * Vector3.up;
            float rx = new Vector2(right.x, right.z).magnitude * halfW;
            float uz = new Vector2(up.x, up.z).magnitude * halfH;
            padding = Mathf.Max(rx, uz);
        }
        else
        {
            float vHalf = Mathf.Deg2Rad * Mathf.Clamp(state.Lens.FieldOfView, 1f, 179f) * 0.5f;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Max(0.01f, state.Lens.Aspect));

            Vector3[] dirs =
            {
                new Vector3(Mathf.Tan(hHalf),  Mathf.Tan(vHalf),  1f),
                new Vector3(-Mathf.Tan(hHalf), Mathf.Tan(vHalf),  1f),
                new Vector3(Mathf.Tan(hHalf), -Mathf.Tan(vHalf),  1f),
                new Vector3(-Mathf.Tan(hHalf),-Mathf.Tan(vHalf),  1f)
            };

            float maxDist = 0f;
            int hitCount = 0;
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3 worldDir = (camRot * dirs[i]).normalized;
                float denom = worldDir.y;
                if (Mathf.Abs(denom) < 0.0001f) continue;
                float t = (planeY - camPos.y) / denom;
                if (t <= 0.01f) continue;

                Vector3 hit = camPos + worldDir * t;
                Vector2 d = new Vector2(hit.x - camPos.x, hit.z - camPos.z);
                maxDist = Mathf.Max(maxDist, d.magnitude);
                hitCount++;
            }

            if (hitCount > 0)
                padding = maxDist;
        }

        padding = Mathf.Clamp(padding, minAutoPadding, maxAutoPadding);
        return padding;
    }

    private Vector2 ToPaddingVector(float padding)
    {
        switch (paddingAxis)
        {
            case PaddingAxisMode.XOnly:
                return new Vector2(padding, 0f);
            case PaddingAxisMode.ZOnly:
                return new Vector2(0f, padding);
            default:
                return new Vector2(padding, padding);
        }
    }

    private Vector2 GetGizmoPadding()
    {
        if (_hasLastPadding) return _lastPadding;
        float autoPad = 0f;
        if (autoPadding)
            autoPad = Mathf.Clamp(fallbackPadding, minAutoPadding, maxAutoPadding);
        return ToPaddingVector(extraPadding + autoPad);
    }

    private void DrawClampGizmo()
    {
        if (!drawClampGizmo) return;
        if (!ResolvePlayArea()) return;
        Vector2 padding = GetGizmoPadding();
        playArea.DrawGizmo(padding, clampGizmoColor, clampGizmoYOffset, clampGizmoSegments);
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected) return;
        DrawClampGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected) return;
        DrawClampGizmo();
    }
}
