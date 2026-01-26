using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneLoadButton : MonoBehaviour
{
    [Header("씬 로드")]
    [SerializeField] private string sceneName;
    [SerializeField] private LoadSceneMode mode = LoadSceneMode.Single;
    [SerializeField] private bool useAsync = true;
    [SerializeField] private float minAsyncLoadTime = 0f;
    [SerializeField] private bool useUnscaledTime = true;

    private bool _loading;
    public void Load()
    {
        if (_loading) return;
        if (string.IsNullOrEmpty(sceneName)) return;

        if (!useAsync)
        {
            SceneManager.LoadScene(sceneName, mode);
            return;
        }

        if (minAsyncLoadTime <= 0f)
        {
            SceneManager.LoadSceneAsync(sceneName, mode);
            return;
        }

        StartCoroutine(LoadAsync());
    }
    private IEnumerator LoadAsync()
    {
        _loading = true;

        var op = SceneManager.LoadSceneAsync(sceneName, mode);
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
