using System.Collections.Generic;
using UnityEngine;

public sealed class DamageNumberService : MonoBehaviour
{
    public static DamageNumberService Instance { get; private set; }

    [Header("UI 설정")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform root;
    [SerializeField] private DamageNumberItem itemPrefab;
    [SerializeField] private Camera targetCamera;

    [Header("스폰")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 40f);
    [SerializeField] private Vector2 randomOffset = new Vector2(12f, 12f);
    [SerializeField] private int prewarm = 12;

    private readonly Queue<DamageNumberItem> _pool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (root == null && canvas != null) root = canvas.GetComponent<RectTransform>();
        Prewarm();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void TryShow(int amount, Vector3 worldPos)
    {
        if (Instance == null) return;
        Instance.ShowDamage(amount, worldPos);
    }

    public void ShowDamage(int amount, Vector3 worldPos)
    {
        if (amount <= 0) return;
        if (itemPrefab == null || root == null) return;

        Camera cam = targetCamera != null ? targetCamera : (canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main);
        if (cam == null) return;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        Camera uiCam = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCam, out Vector2 local);
        local += screenOffset;

        if (randomOffset.sqrMagnitude > 0.01f)
        {
            local.x += Random.Range(-randomOffset.x, randomOffset.x);
            local.y += Random.Range(-randomOffset.y, randomOffset.y);
        }

        var item = GetItem();
        if (item == null) return;
        item.transform.SetParent(root, false);
        item.gameObject.SetActive(true);
        item.Play(amount, local, ReturnItem);
    }

    private void Prewarm()
    {
        if (itemPrefab == null) return;
        int count = Mathf.Max(0, prewarm);
        for (int i = 0; i < count; i++)
            ReturnItem(CreateItem());
    }

    private DamageNumberItem GetItem()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();
        return CreateItem();
    }

    private DamageNumberItem CreateItem()
    {
        if (itemPrefab == null) return null;
        var item = Instantiate(itemPrefab, root);
        item.gameObject.SetActive(false);
        return item;
    }

    private void ReturnItem(DamageNumberItem item)
    {
        if (item == null) return;
        item.gameObject.SetActive(false);
        _pool.Enqueue(item);
    }
}
