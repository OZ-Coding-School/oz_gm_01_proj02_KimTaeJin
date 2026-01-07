using DG.Tweening;
using UnityEngine;

public static class UiTweenUtil
{
    public static void Pop(Transform t)
    {
        t.DOKill();
        t.localScale = Vector3.zero;
        t.DOScale(1f, 0.22f).SetEase(Ease.OutBack);
    }

    public static void ClickJelly(Transform t)
    {
        t.DOKill();
        t.DOPunchScale(new Vector3(0.12f, -0.08f, 0.12f), 0.16f, 10, 1f);
    }
}
