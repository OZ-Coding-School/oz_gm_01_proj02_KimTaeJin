using System;
using UnityEngine;

public sealed class SkillPanelSlots : MonoBehaviour
{
    [SerializeField] private SkillSlotUI[] slots;
    [SerializeField] private PlayerSkillSystem skillSystem;

    private void Awake()
    {
        ResolveSlots();
    }

    private void OnDestroy()
    {
        if (skillSystem != null)
            skillSystem.SkillsChanged -= OnSkillsChanged;
    }

    public void Bind(PlayerSkillSystem system)
    {
        ResolveSlots();
        if (skillSystem == system) return;
        if (skillSystem != null)
            skillSystem.SkillsChanged -= OnSkillsChanged;
        skillSystem = system;
        if (skillSystem != null)
            skillSystem.SkillsChanged += OnSkillsChanged;
        Refresh(null);
    }

    public void Refresh(PlayerSkillOptionSO previewOption)
    {
        ResolveSlots();
        if (skillSystem == null || slots == null || slots.Length == 0) return;

        var skills = skillSystem.Skills;
        int count = skills != null ? skills.Count : 0;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            if (i < count)
                slot.SetSkill(PlayerSkillSystem.SkillSnapshot.FromState(skills[i]), false, default, false, 0);
            else
                slot.Clear();
        }

        if (previewOption == null) return;

        if (previewOption.optionType == PlayerSkillOptionSO.OptionType.UpgradeSkill)
        {
            if (skillSystem.TryGetPreview(previewOption, out PlayerSkillSystem.SkillSnapshot cur, out PlayerSkillSystem.SkillSnapshot preview))
            {
                int limit = Mathf.Min(count, slots.Length);
                for (int i = 0; i < limit; i++)
                {
                    if (skills[i].Definition == cur.Definition && slots[i] != null)
                        slots[i].SetSkill(cur, true, preview, true, preview.Level);
                }
            }
            return;
        }

        if (previewOption.optionType == PlayerSkillOptionSO.OptionType.AddSkill)
        {
            if (!skillSystem.CanApplyOption(previewOption)) return;
            if (!skillSystem.TryGetPreview(previewOption, out _, out PlayerSkillSystem.SkillSnapshot preview))
                return;

            if (count >= slots.Length) return;
            int emptyIndex = count;
            if (slots[emptyIndex] != null)
                slots[emptyIndex].SetSkill(preview, false, default, true, preview.Level);
        }
    }

    private void OnSkillsChanged()
    {
        Refresh(null);
    }

    private void ResolveSlots()
    {
        if (slots != null && slots.Length > 0 && !HasNull(slots)) return;
        slots = GetComponentsInChildren<SkillSlotUI>(true);
        if (slots == null || slots.Length == 0) return;
        Array.Sort(slots, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
    }

    private static bool HasNull(SkillSlotUI[] list)
    {
        for (int i = 0; i < list.Length; i++)
            if (list[i] == null) return true;
        return false;
    }
}
