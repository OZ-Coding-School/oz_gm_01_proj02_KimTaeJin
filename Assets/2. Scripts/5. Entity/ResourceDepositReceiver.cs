using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResourceDepositReceiver : MonoBehaviour
{
    [SerializeField] private bool useRunScope = true;
    [SerializeField] private ResourceProgression progressionOverride;
    [SerializeField] private bool debugLog = false;

    private static readonly List<ResourceDepositReceiver> _instances = new();
    public static IReadOnlyList<ResourceDepositReceiver> Instances => _instances;

    private void OnEnable()
    {
        if (!_instances.Contains(this))
            _instances.Add(this);
    }

    private void OnDisable()
    {
        _instances.Remove(this);
    }

    public void Deposit(IReadOnlyList<DropItem> items)
    {
        if (items == null || items.Count == 0) return;

        ResourceProgression progression = ResolveProgression();
        if (progression == null) return;

        int wood = 0;
        int stone = 0;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null) continue;
            int amt = Mathf.Max(0, item.Amount);
            if (amt == 0) continue;

            switch (item.ResourceType)
            {
                case ResourceType.Wood:
                    wood += amt;
                    break;
                case ResourceType.Stone:
                    stone += amt;
                    break;
            }
        }

        if (wood > 0) progression.AddResource(ResourceType.Wood, wood);
        if (stone > 0) progression.AddResource(ResourceType.Stone, stone);

        if (debugLog)
            Debug.Log($"[ResourceDepositReceiver] wood={wood} stone={stone} target={gameObject.name}");
    }

    private ResourceProgression ResolveProgression()
    {
        if (progressionOverride != null) return progressionOverride;
        if (!useRunScope) return null;
        return RunScopeLocator.Current != null ? RunScopeLocator.Current.Progression : null;
    }
}
