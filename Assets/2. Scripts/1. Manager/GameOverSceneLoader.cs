using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GameOverSceneLoader : MonoBehaviour
{
    [Header("씬 이동")]
    [SerializeField] private string endSceneName = "GameOver";
    [SerializeField] private float delay = 0.5f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool useAsync = true;
    [SerializeField] private float minAsyncLoadTime = 0f;

    [Header("대상")]
    [SerializeField] private SharedBuildingHealth sharedHealth;
    [SerializeField] private HealthComponent playerHealth;
    [SerializeField] private bool autoFind = true;

    private bool _loading;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        ResolveRefs();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        _loading = false;
    }

    private void OnScopeChanged(RunScope scope)
    {
        ResolveRefs();
    }

    private void Update()
    {
        if (_loading) return;
        if (!IsDead()) return;
        StartCoroutine(LoadEndScene());
    }

    private bool IsDead()
    {
        ResolveRefs();
        bool buildingDead = sharedHealth != null && sharedHealth.IsDead;
        bool playerDead = playerHealth != null && playerHealth.Current <= 0;
        return buildingDead || playerDead;
    }

    private void ResolveRefs()
    {
        if (!autoFind) return;

        var scope = RunScopeLocator.Current;
        if (sharedHealth == null && scope != null)
            sharedHealth = scope.GetComponent<SharedBuildingHealth>();

        if (playerHealth == null && scope != null && scope.Entities?.Player != null)
            playerHealth = scope.Entities.Player.Health;
    }
    private IEnumerator LoadEndScene()
    {
        _loading = true;

        if (delay > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(delay);
            else
                yield return new WaitForSeconds(delay);
        }

        if (string.IsNullOrEmpty(endSceneName))
        {
            _loading = false;
            yield break;
        }

        if (!useAsync)
        {
            SceneManager.LoadScene(endSceneName);
            yield break;
        }

        var op = SceneManager.LoadSceneAsync(endSceneName);
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

