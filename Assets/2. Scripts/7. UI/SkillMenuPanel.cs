using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SkillMenuPanel : MonoBehaviour
{
    [Header("Panel Root")]
    [SerializeField] private GameObject root;

    [Header("Fade")]
    [SerializeField] private CanvasGroup dimGroup;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private float fadeDuration = 0.18f;

    [Header("Slots")]
    [SerializeField] private SkillPanelSlots slots;

    [Header("Options (3)")]
    [SerializeField] private SkillOptionButton[] optionButtons;
    [SerializeField] private SkillOptionPreviewPanel previewPanel;

    [Header("Catalog")]
    [SerializeField] private SkillOptionCatalogSO optionCatalog;
    [SerializeField] private bool allowDuplicates = false;
    [SerializeField] private bool forceIncludeAddSkill = true;

    [Header("Input")]
    [SerializeField] private KeyCode confirmKey = KeyCode.Return;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;

    [Header("Pause")]
    [SerializeField] private float pauseTimeScale = 0f;

    private RunScope _scope;
    private PlayerSkillSystem _skills;
    private readonly List<PlayerSkillOptionSO> _draft = new();
    private int _selectedIndex = -1;
    private Tween _panelFade;
    private Tween _dimFade;
    private bool _pausedForMenu;
    private float _prevTimeScale = 1f;

    public bool IsOpen => root != null && root.activeInHierarchy;
    public event Action<bool> OpenStateChanged;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (slots == null) slots = GetComponentInChildren<SkillPanelSlots>(true);
        if (previewPanel == null) previewPanel = GetComponentInChildren<SkillOptionPreviewPanel>(true);
        ResolvePanelGroup();
        ResolveOptionButtons();
        BindOptionButtons();

        root.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;
        HandleNavigation();

        if (Input.GetKeyDown(cancelKey))
            Close();
    }

    public void Open()
    {
        TryBind();
        if (_skills == null) return;

        ShowPanel(true);
        PauseForMenu();
        Draft();
        ClearSelection();
        EnsureDefaultSelection();
        slots?.Bind(_skills);
        slots?.Refresh(GetSelectedOption());
        previewPanel?.ShowOption(GetSelectedOption());
    }

    public void Close()
    {
        ShowPanel(false);
        ClearSelection();
        previewPanel?.Hide();
        ClearPause();
    }

    private void TryBind()
    {
        if (_scope == null) _scope = RunScopeLocator.Current;
        if (_scope == null || _scope.Entities == null) return;
        _skills = _scope.Entities.Player != null ? _scope.Entities.Player.Skills : null;
    }

    private void HandleNavigation()
    {
        ResolveOptionButtons();
        if (optionButtons == null || optionButtons.Length == 0) return;
        if (_selectedIndex < 0 || !IsSelectable(_selectedIndex))
            EnsureDefaultSelection();

        int dir = 0;
        if (Input.GetKeyDown(leftKey)) dir = -1;
        if (Input.GetKeyDown(rightKey)) dir = 1;
        if (dir != 0)
        {
            ClearUnitySelection();
            int next = FindNextSelectable(_selectedIndex, dir);
            if (next >= 0) SetSelectedIndex(next);
        }

        if (Input.GetKeyDown(confirmKey) && _selectedIndex >= 0)
        {
            ClearUnitySelection();
            var btn = optionButtons[_selectedIndex];
            if (btn != null && btn.Option != null)
                ApplyOption(btn.Option);
        }
    }

    private void Draft()
    {
        _draft.Clear();
        if (optionCatalog == null || optionCatalog.Options == null || optionCatalog.Options.Length == 0) return;
        ResolveOptionButtons();
        if (optionButtons == null || optionButtons.Length == 0) return;

        var pool = new List<PlayerSkillOptionSO>();
        var addPool = new List<PlayerSkillOptionSO>();
        for (int i = 0; i < optionCatalog.Options.Length; i++)
        {
            var opt = optionCatalog.Options[i];
            if (opt == null) continue;
            if (_skills != null && !_skills.CanApplyOption(opt)) continue;
            pool.Add(opt);
            if (opt.optionType == PlayerSkillOptionSO.OptionType.AddSkill)
                addPool.Add(opt);
        }

        if (pool.Count == 0) return;

        int count = Mathf.Min(3, optionButtons != null ? optionButtons.Length : 3);
        bool dup = allowDuplicates || pool.Count < count;

        if (forceIncludeAddSkill && addPool.Count > 0)
        {
            int addIdx = UnityEngine.Random.Range(0, addPool.Count);
            var pickedAdd = addPool[addIdx];
            _draft.Add(pickedAdd);
            if (!dup) pool.Remove(pickedAdd);
        }

        for (int i = _draft.Count; i < count; i++)
        {
            if (pool.Count == 0) break;
            int idx = UnityEngine.Random.Range(0, pool.Count);
            var picked = pool[idx];
            _draft.Add(picked);
            if (!dup) pool.RemoveAt(idx);
        }

        for (int i = 0; i < optionButtons.Length; i++)
        {
            var btn = optionButtons[i];
            if (btn == null) continue;
            if (i < _draft.Count)
            {
                btn.gameObject.SetActive(true);
                btn.SetOption(_draft[i]);
                btn.SetInteractable(true);
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }
    }

    private void ApplyOption(PlayerSkillOptionSO option)
    {
        if (_skills == null || option == null) return;
        if (!_skills.ApplyOption(option)) return;
        Close();
    }

    private bool IsSelectable(int index)
    {
        if (index < 0 || index >= optionButtons.Length) return false;
        var btn = optionButtons[index];
        return btn != null && btn.gameObject.activeInHierarchy && btn.Option != null;
    }

    private int FindNextSelectable(int start, int dir)
    {
        int len = optionButtons.Length;
        int idx = start;
        for (int i = 0; i < len; i++)
        {
            idx = (idx + dir + len) % len;
            if (IsSelectable(idx)) return idx;
        }
        return -1;
    }

    private void SetSelectedIndex(int index)
    {
        _selectedIndex = index;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            var btn = optionButtons[i];
            if (btn == null) continue;
            btn.SetSelected(i == _selectedIndex);
        }
        slots?.Refresh(GetSelectedOption());
        previewPanel?.ShowOption(GetSelectedOption());
    }

    private void OnOptionSelected(SkillOptionButton btn)
    {
        if (btn == null) return;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == btn)
            {
                SetSelectedIndex(i);
                break;
            }
        }
    }

    private void OnOptionConfirmed(SkillOptionButton btn, PlayerSkillOptionSO option)
    {
        ApplyOption(option);
    }

    private void EnsureDefaultSelection()
    {
        int idx = FindNextSelectable(-1, 1);
        if (idx >= 0) SetSelectedIndex(idx);
    }

    private void ClearSelection()
    {
        _selectedIndex = -1;
        if (optionButtons == null) return;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            var btn = optionButtons[i];
            if (btn == null) continue;
            btn.SetSelected(false);
        }
        previewPanel?.Hide();
    }

    private PlayerSkillOptionSO GetSelectedOption()
    {
        if (_selectedIndex < 0 || optionButtons == null || _selectedIndex >= optionButtons.Length) return null;
        return optionButtons[_selectedIndex]?.Option;
    }

    private void ShowPanel(bool show)
    {
        if (root == null) return;
        ResolvePanelGroup();

        if (panelGroup == null && dimGroup == null)
        {
            root.SetActive(show);
            return;
        }

        if (show)
        {
            root.SetActive(true);
            OpenStateChanged?.Invoke(true);
            if (panelGroup != null)
            {
                panelGroup.alpha = 0f;
                panelGroup.interactable = true;
                panelGroup.blocksRaycasts = true;
            }
            if (dimGroup != null)
            {
                dimGroup.alpha = 0f;
                dimGroup.blocksRaycasts = true;
            }

            _panelFade?.Kill();
            _dimFade?.Kill();

            if (panelGroup != null)
                _panelFade = panelGroup.DOFade(1f, fadeDuration).SetUpdate(true);
            if (dimGroup != null)
                _dimFade = dimGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        }
        else
        {
            if (panelGroup != null)
            {
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
            }
            if (dimGroup != null)
                dimGroup.blocksRaycasts = false;

            _panelFade?.Kill();
            _dimFade?.Kill();

            Tween last = null;
            if (panelGroup != null)
                last = panelGroup.DOFade(0f, fadeDuration).SetUpdate(true);
            if (dimGroup != null)
                _dimFade = dimGroup.DOFade(0f, fadeDuration).SetUpdate(true);

            if (last != null)
                last.OnComplete(() =>
                {
                    if (root != null) root.SetActive(false);
                    OpenStateChanged?.Invoke(false);
                });
            else
            {
                root.SetActive(false);
                OpenStateChanged?.Invoke(false);
            }
        }
    }

    private void PauseForMenu()
    {
        if (_pausedForMenu) return;
        _prevTimeScale = Time.timeScale;
        Time.timeScale = pauseTimeScale;
        _pausedForMenu = true;
    }

    private void ClearPause()
    {
        if (!_pausedForMenu) return;
        var scope = _scope != null ? _scope : RunScopeLocator.Current;
        bool buildModeOn = scope != null && scope.Events != null && scope.Events.IsBuildMode;
        if (!buildModeOn)
            Time.timeScale = Mathf.Approximately(_prevTimeScale, 0f) ? 1f : _prevTimeScale;
        _pausedForMenu = false;
    }

    private void ClearUnitySelection()
    {
        if (EventSystem.current == null) return;
        if (EventSystem.current.currentSelectedGameObject == null) return;
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void ResolveOptionButtons()
    {
        if (optionButtons != null && optionButtons.Length > 0 && !HasNull(optionButtons)) return;
        optionButtons = GetComponentsInChildren<SkillOptionButton>(true);
        if (optionButtons == null || optionButtons.Length == 0) return;
        Array.Sort(optionButtons, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
        BindOptionButtons();
    }

    private void ResolvePanelGroup()
    {
        if (panelGroup != null) return;
        if (root != null)
            panelGroup = root.GetComponent<CanvasGroup>();
        if (panelGroup == null)
            panelGroup = GetComponent<CanvasGroup>();
    }

    private static bool HasNull(SkillOptionButton[] list)
    {
        for (int i = 0; i < list.Length; i++)
            if (list[i] == null) return true;
        return false;
    }

    private void BindOptionButtons()
    {
        if (optionButtons == null) return;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            var btn = optionButtons[i];
            if (btn == null) continue;
            btn.Selected -= OnOptionSelected;
            btn.Selected += OnOptionSelected;
            btn.Confirmed -= OnOptionConfirmed;
            btn.Confirmed += OnOptionConfirmed;
        }
    }
}
