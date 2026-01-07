using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class JellyPunch : MonoBehaviour
{
    [Header("Target (비워두면 자기 자신)")]
    [SerializeField] private Transform visual;

    [Header("Jelly Tuning")]
    [SerializeField] private Vector3 punchScale = new Vector3(0.18f, -0.12f, 0.18f);
    [SerializeField] private float duration = 0.18f;
    [SerializeField] private int vibrato = 10;
    [SerializeField, Range(0f, 1.5f)] private float elasticity = 1f;

    private Vector3 _baseScale;
    private bool _inited;

    private void Awake()
    {
        if (visual == null) visual = transform;
        _baseScale = visual.localScale;
        _inited = true;
    }

    public void Play()
    {
        if (!_inited) Awake();

        visual.DOKill();
        visual.localScale = _baseScale;

        visual.DOPunchScale(punchScale, duration, vibrato, elasticity);
    }

    private void OnDisable()
    {
        if (visual != null)
        {
            visual.DOKill();
            visual.localScale = _baseScale;
        }
    }
}
