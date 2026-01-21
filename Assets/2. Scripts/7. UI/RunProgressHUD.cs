using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class RunProgressHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text nextStopText;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private string nextStopFormat = "{0:0}";
    [SerializeField] private string goalFormat = "{0:0}";
    [SerializeField] private string noneText = "-";

    [Header("Refs")]
    [SerializeField] private Transform progressSubject;
    [SerializeField] private Transform goal;
    [SerializeField] private Transform startPoint;
    [SerializeField] private bool useHouseDriftDirection = true;
    [SerializeField] private Vector3 fallbackAxis = Vector3.back;
    [SerializeField] private float distanceScale = 1f;
    [SerializeField] private bool invertProgress = false;

    [Header("Rest Stops")]
    [SerializeField] private bool autoFindRestStops = true;
    [SerializeField] private RestStopMarker[] restStops;

    private Vector3 _axis;
    private bool _axisResolved;
    private Vector3 _startPos;
    private HouseDrift _houseDrift;

    private void Awake()
    {
        ResolveRefs();
        CaptureStart();
    }

    private void OnEnable()
    {
        ResolveRefs();
        CaptureStart();
    }

    private void Update()
    {
        if (progressSubject == null || goal == null) return;
        ResolveAxis();

        float total = Vector3.Dot(goal.position - _startPos, _axis);
        if (total <= 0.0001f)
            return;

        float current = Vector3.Dot(progressSubject.position - _startPos, _axis);
        current = Mathf.Clamp(current, 0f, total);

        float progress01 = Mathf.Clamp01(current / total);
        if (invertProgress) progress01 = 1f - progress01;

        if (progressSlider != null)
            progressSlider.SetValueWithoutNotify(progress01);

        UpdateTexts(current, total);
    }

    private void ResolveRefs()
    {
        if (progressSubject == null)
        {
            _houseDrift = FindObjectOfType<HouseDrift>();
            if (_houseDrift != null)
                progressSubject = _houseDrift.transform;
            else if (RunScopeLocator.Current != null && RunScopeLocator.Current.Grid != null)
                progressSubject = RunScopeLocator.Current.Grid.Anchor;
        }

        if (_houseDrift == null)
            _houseDrift = FindObjectOfType<HouseDrift>();

        if (autoFindRestStops && (restStops == null || restStops.Length == 0))
            restStops = FindObjectsOfType<RestStopMarker>(true);
    }

    private void CaptureStart()
    {
        if (startPoint != null)
            _startPos = startPoint.position;
        else if (progressSubject != null)
            _startPos = progressSubject.position;
    }

    private void ResolveAxis()
    {
        if (_axisResolved) return;
        _axisResolved = true;

        Vector3 axis = fallbackAxis;
        if (useHouseDriftDirection && _houseDrift != null)
        {
            Vector3 d = _houseDrift.Direction;
            if (d.sqrMagnitude > 0.0001f)
                axis = d;
        }

        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.back;

        _axis = axis.normalized;
        if (goal != null && Vector3.Dot(goal.position - _startPos, _axis) < 0f)
            _axis = -_axis;
    }

    private void UpdateTexts(float current, float total)
    {
        float goalDist = (total - current) * distanceScale;
        if (goalText != null)
            goalText.text = string.Format(goalFormat, Mathf.Max(0f, goalDist));

        float nextStopDist = GetNextStopDistance(current) * distanceScale;
        if (nextStopText != null)
        {
            if (nextStopDist < 0f)
                nextStopText.text = noneText;
            else
                nextStopText.text = string.Format(nextStopFormat, Mathf.Max(0f, nextStopDist));
        }
    }

    private float GetNextStopDistance(float current)
    {
        if (restStops == null || restStops.Length == 0) return -1f;

        float best = float.MaxValue;
        for (int i = 0; i < restStops.Length; i++)
        {
            var stop = restStops[i];
            if (stop == null || stop.Point == null) continue;
            float stopDist = Vector3.Dot(stop.Point.position - _startPos, _axis);
            float remain = stopDist - current;
            if (remain < 0f) continue;
            if (remain < best) best = remain;
        }

        return best == float.MaxValue ? -1f : best;
    }
}
