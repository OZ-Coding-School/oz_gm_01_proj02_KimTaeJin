using UnityEngine;
using TMPro;

public sealed class InGameHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;

    private RunScope _scope;

    private void OnEnable()
    {
        RunScopeLocator.Changed += OnScopeChanged;
        TryBind();
    }

    private void OnDisable()
    {
        RunScopeLocator.Changed -= OnScopeChanged;
        Unbind();
    }

    private void TryBind()
    {
        Unbind();

        _scope = RunScopeLocator.Current;
        if (_scope == null) return;

        if (_scope.Economy == null) return;

        _scope.Economy.OnGoldChanged += OnGoldChanged;
        OnGoldChanged(_scope.Economy.Gold);
    }

    private void Unbind()
    {
        if (_scope != null && _scope.Economy != null)
            _scope.Economy.OnGoldChanged -= OnGoldChanged;

        _scope = null;
    }

    private void OnScopeChanged(RunScope scope)
    {
        TryBind();
    }

    private void OnGoldChanged(int gold)
    {
        if (goldText != null)
            goldText.text = $"Gold : {gold}";
    }
}
