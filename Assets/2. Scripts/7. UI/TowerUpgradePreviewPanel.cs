using TMPro;
using UnityEngine;

public sealed class TowerUpgradePreviewPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private CanvasGroup panelGroup;

    [Header("표시 색상")]
    [SerializeField] private Color changedValueColor = new Color(0.3f, 0.95f, 0.3f, 1f);
    [SerializeField] private Color deltaValueColor = new Color(1f, 0.9f, 0.25f, 1f);

    [Header("Refs")]
    [SerializeField] private TowerPlacementController controller;
    [SerializeField] private GridDataService dataService;

    private void Awake()
    {
        if (root == null) root = gameObject;
        if (panelGroup == null && root != null)
            panelGroup = root.GetComponent<CanvasGroup>();
        if (panelGroup == null && root == gameObject)
            panelGroup = root.AddComponent<CanvasGroup>();
        if (controller == null) controller = FindObjectOfType<TowerPlacementController>(true);
        if (dataService == null) dataService = FindObjectOfType<GridDataService>(true);
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (controller != null)
        {
            controller.OnCellHoverChanged += OnCellHoverChanged;
            controller.OnPlacementCanceled += OnPlacementCanceled;
            controller.OnPlacementConfirmed += OnPlacementConfirmed;
        }
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.OnCellHoverChanged -= OnCellHoverChanged;
            controller.OnPlacementCanceled -= OnPlacementCanceled;
            controller.OnPlacementConfirmed -= OnPlacementConfirmed;
        }
        SetVisible(false);
    }

    private void OnCellHoverChanged(Vector3Int cell)
    {
        if (controller == null || dataService == null || controller.Selected == null)
        {
            SetVisible(false);
            return;
        }

        GridDataService.PlacementResult result = dataService.EvaluatePlacement(controller.Selected, cell);
        if (!result.isUpgrade || result.previewDef == null || result.existingDef == null)
        {
            SetVisible(false);
            return;
        }

        UpdateTexts(result.existingDef, result.previewDef);
        SetVisible(true);
    }

    private void OnPlacementCanceled()
    {
        SetVisible(false);
    }

    private void OnPlacementConfirmed(bool ok)
    {
        SetVisible(false);
    }

    private void UpdateTexts(TowerDefinitionSO cur, TowerDefinitionSO next)
    {
        if (damageText != null)
            damageText.text = FormatStat("공격력", cur.damage, next.damage, true);
        if (speedText != null)
        {
            float curSpeed = cur.attackSpeed;
            float nextSpeed = next.attackSpeed;
            speedText.text = FormatStat("공격속도", curSpeed, nextSpeed, false);
        }
        if (rangeText != null)
            rangeText.text = FormatStat("사거리", cur.range, next.range, false);
    }


    private string FormatStat(string label, float cur, float next, bool integer)
    {
        string curText = integer ? Mathf.RoundToInt(cur).ToString() : cur.ToString("0.0");
        string nextText = integer ? Mathf.RoundToInt(next).ToString() : next.ToString("0.0");
        float delta = next - cur;

        if (Mathf.Abs(delta) < 0.0001f)
            return $"{label} {curText}";

        string deltaText = integer
            ? $"{(delta >= 0 ? "+" : string.Empty)}{Mathf.RoundToInt(delta)}"
            : $"{(delta >= 0 ? "+" : string.Empty)}{delta:0.0}";

        string coloredNext = Colorize(nextText, changedValueColor);
        string coloredDelta = Colorize(deltaText, deltaValueColor);
        return $"{label} {curText} -> {coloredNext} ({coloredDelta})";
    }

    private static string Colorize(string text, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }

    private void SetVisible(bool on)
    {
        if (root == null) return;
        if (panelGroup != null && root == gameObject)
        {
            panelGroup.alpha = on ? 1f : 0f;
            panelGroup.interactable = on;
            panelGroup.blocksRaycasts = on;
            return;
        }

        root.SetActive(on);
    }
}
