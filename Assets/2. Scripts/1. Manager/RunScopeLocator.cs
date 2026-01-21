using System;

public static class RunScopeLocator
{
    public static RunScope Current { get; private set; }
    public static event Action<RunScope> Changed;

    internal static void SetCurrent(RunScope scope, bool forceNotify = false)
    {
        if (!forceNotify && ReferenceEquals(Current, scope)) return;
        Current = scope;
        Changed?.Invoke(Current);
    }
}
