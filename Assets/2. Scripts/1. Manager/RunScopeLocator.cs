using System;

public static class RunScopeLocator
{
    public static RunScope Current { get; private set; }
    public static event Action<RunScope> Changed;

    internal static void SetCurrent(RunScope scope)
    {
        if (ReferenceEquals(Current, scope)) return;
        Current = scope;
        Changed?.Invoke(Current);
    }
}
