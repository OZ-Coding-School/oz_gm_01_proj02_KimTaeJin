using System.Collections.Generic;
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

    [Header("중간 지점")]
    [SerializeField] private bool autoFindMidPoints = true;
    [SerializeField] private RunProgressPoint[] midPoints;

    [Header("Progress Markers")]
    [SerializeField] private RectTransform markerTrack;
    [SerializeField] private RectTransform markerRoot;
    [SerializeField] private GameObject restStopMarkerPrefab;
    [SerializeField] private RectTransform midPointMarkerRoot;
    [SerializeField] private GameObject midPointMarkerPrefab;
    [SerializeField] private RectTransform goalMarker;
    [SerializeField] private float markerPadding = 6f;
    [SerializeField] private bool markerTopToBottom = true;
    [SerializeField] private bool autoBuildMarkers = true;

    private Vector3 _axis;
    private bool _axisResolved;
    private Vector3 _startPos;
    private bool _startCaptured;
    private HouseDrift _houseDrift;
    private readonly List<RectTransform> _restStopMarkers = new();
    private readonly List<RectTransform> _midPointMarkers = new();
    private bool _markersDirty;

    private void Awake()
    {
        ResolveRefs();
        CaptureStart();
        MarkMarkersDirty();
    }

    private void OnEnable()
    {
        ResolveRefs();
        CaptureStart();
        MarkMarkersDirty();
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
        UpdateMarkers(total);
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

        if (autoFindRestStops)
            MarkMarkersDirty();

        if (autoFindMidPoints && (midPoints == null || midPoints.Length == 0))
            midPoints = FindObjectsOfType<RunProgressPoint>(true);

        if (autoFindMidPoints)
            MarkMarkersDirty();
    }

    private void CaptureStart()
    {
        if (startPoint != null)
        {
            _startPos = startPoint.position;
            _startCaptured = true;
            return;
        }

        if (_startCaptured) return;
        if (progressSubject != null)
        {
            _startPos = progressSubject.position;
            _startCaptured = true;
        }
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

    private void MarkMarkersDirty()
    {
        _markersDirty = true;
    }

    private void UpdateMarkers(float total)
    {
        if (markerTrack == null) return;
        if (total <= 0.0001f) return;

        if (autoBuildMarkers)
            EnsureMarkers();

        float yMin = markerTrack.rect.yMin + markerPadding;
        float yMax = markerTrack.rect.yMax - markerPadding;
        if (yMax < yMin)
        {
            float tmp = yMax;
            yMax = yMin;
            yMin = tmp;
        }

        for (int i = 0; i < _restStopMarkers.Count; i++)
        {
            var marker = _restStopMarkers[i];
            var stop = (restStops != null && i < restStops.Length) ? restStops[i] : null;
            if (marker == null || stop == null || stop.Point == null) continue;

            float stopDist = Vector3.Dot(stop.Point.position - _startPos, _axis);
            float t = Mathf.Clamp01(stopDist / total);
            float y = markerTopToBottom ? Mathf.Lerp(yMax, yMin, t) : Mathf.Lerp(yMin, yMax, t);
            Vector2 pos = marker.anchoredPosition;
            pos.y = y;
            marker.anchoredPosition = pos;
        }

        for (int i = 0; i < _midPointMarkers.Count; i++)
        {
            var marker = _midPointMarkers[i];
            var point = (midPoints != null && i < midPoints.Length) ? midPoints[i] : null;
            if (marker == null || point == null || point.Point == null) continue;

            float dist = Vector3.Dot(point.Point.position - _startPos, _axis);
            float t = Mathf.Clamp01(dist / total);
            float y = markerTopToBottom ? Mathf.Lerp(yMax, yMin, t) : Mathf.Lerp(yMin, yMax, t);
            Vector2 pos = marker.anchoredPosition;
            pos.y = y;
            marker.anchoredPosition = pos;
        }

        if (goalMarker != null)
        {
            float y = markerTopToBottom ? yMin : yMax;
            Vector2 pos = goalMarker.anchoredPosition;
            pos.y = y;
            goalMarker.anchoredPosition = pos;
        }
    }

    private void EnsureMarkers()
    {
        int restCount = restStops != null ? restStops.Length : 0;
        int midCount = midPoints != null ? midPoints.Length : 0;
        if (!_markersDirty && _restStopMarkers.Count == restCount && _midPointMarkers.Count == midCount)
            return;
        _markersDirty = false;

        EnsureMarkerSet(_restStopMarkers, restCount, markerRoot, restStopMarkerPrefab);
        EnsureMarkerSet(_midPointMarkers, midCount, ResolveMidPointRoot(), ResolveMidPointPrefab());

        ApplyMarkerVisuals();
    }

    private void ClearMarkers()
    {
        ClearMarkerSet(_restStopMarkers);
        ClearMarkerSet(_midPointMarkers);
    }

    private void ApplyMarkerVisuals()
    {
        ApplyRestStopMarkerVisuals();
        ApplyMidPointMarkerVisuals();
    }

    private void ApplyRestStopMarkerVisuals()
    {
        if (restStops == null || restStops.Length == 0) return;

        for (int i = 0; i < _restStopMarkers.Count; i++)
        {
            var marker = _restStopMarkers[i];
            if (marker == null) continue;
            var view = marker.GetComponent<RestStopMarkerView>();
            if (view == null) continue;
            var stop = (i < restStops.Length) ? restStops[i] : null;
            view.Apply(stop);
        }
    }

    private void ApplyMidPointMarkerVisuals()
    {
        if (midPoints == null || midPoints.Length == 0) return;

        for (int i = 0; i < _midPointMarkers.Count; i++)
        {
            var marker = _midPointMarkers[i];
            if (marker == null) continue;
            var view = marker.GetComponent<RestStopMarkerView>();
            if (view == null) continue;
            var point = (i < midPoints.Length) ? midPoints[i] : null;
            view.Apply(point);
        }
    }

    private RectTransform ResolveMidPointRoot()
    {
        return midPointMarkerRoot != null ? midPointMarkerRoot : markerRoot;
    }

    private GameObject ResolveMidPointPrefab()
    {
        return midPointMarkerPrefab != null ? midPointMarkerPrefab : restStopMarkerPrefab;
    }

    private void EnsureMarkerSet(List<RectTransform> list, int targetCount, RectTransform root, GameObject prefab)
    {
        if (root == null || prefab == null || targetCount <= 0)
        {
            ClearMarkerSet(list);
            return;
        }

        for (int i = list.Count - 1; i >= targetCount; i--)
        {
            if (list[i] != null)
                Destroy(list[i].gameObject);
            list.RemoveAt(i);
        }

        for (int i = list.Count; i < targetCount; i++)
        {
            var go = Instantiate(prefab, root);
            var rect = go.GetComponent<RectTransform>();
            if (rect != null)
                list.Add(rect);
            else
                Destroy(go);
        }
    }

    private void ClearMarkerSet(List<RectTransform> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                Destroy(list[i].gameObject);
        }
        list.Clear();
    }
}
