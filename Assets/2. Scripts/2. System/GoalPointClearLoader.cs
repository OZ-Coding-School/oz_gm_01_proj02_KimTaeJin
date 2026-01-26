using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GoalPointClearLoader : MonoBehaviour
{
    [Header("Goal")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFind = true;
    [SerializeField] private float triggerDistance = 1.5f;
    [SerializeField] private bool useXZOnly = true;

    [Header("Scene")]
    [SerializeField] private string clearSceneName = "GameClear";
    [SerializeField] private float delay = 0f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool useAsync = true;
    [SerializeField] private float minAsyncLoadTime = 0f;

    private bool _loading;

    private void OnEnable()
    {
        ResolveTarget();
    }

    private void Update()
    {
        if (_loading) return;
        if (!IsReached()) return;
        StartCoroutine(LoadClearScene());
    }

    private bool IsReached()
    {
        if (!ResolveTarget()) return false;

        Vector3 a = target.position;
        Vector3 b = transform.position;
        if (useXZOnly)
        {
            a.y = 0f;
            b.y = 0f;
        }

        float r = Mathf.Max(0f, triggerDistance);
        float d2 = (a - b).sqrMagnitude;
        return d2 <= r * r;
    }

    private bool ResolveTarget()
    {
        if (target != null) return true;
        if (!autoFind) return false;

        var house = FindObjectOfType<HouseDrift>();
        if (house != null)
        {
            target = house.transform;
            return true;
        }

        var scope = RunScopeLocator.Current;
        if (scope != null && scope.Grid != null && scope.Grid.Anchor != null)
        {
            target = scope.Grid.Anchor;
            return true;
        }

        return false;
    }

    private IEnumerator LoadClearScene()
    {
        _loading = true;

        if (delay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(delay);
            else
                yield return new WaitForSeconds(delay);
        }

        if (string.IsNullOrEmpty(clearSceneName))
        {
            _loading = false;
            yield break;
        }

        if (!useAsync)
        {
            SceneManager.LoadScene(clearSceneName);
            yield break;
        }

        var op = SceneManager.LoadSceneAsync(clearSceneName);
        if (op == null)
        {
            _loading = false;
            yield break;
        }

        op.allowSceneActivation = false;
        float t = 0f;

        while (!op.isDone)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt < 0f) dt = 0f;
            t += dt;

            if (op.progress >= 0.9f && t >= minAsyncLoadTime)
                op.allowSceneActivation = true;

            yield return null;
        }
    }
}
