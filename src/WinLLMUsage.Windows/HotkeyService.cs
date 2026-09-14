namespace WinLLMUsage.Windows;

public sealed class HotkeyService
{
    public string? Current { get; private set; }

    public bool TryRegister(string? gesture)
    {
        Current = gesture;
        return true;
    }

    public void Unregister() => Current = null;
}

public sealed class ToastNotificationService : Core.Contracts.INotificationService
{
    private bool _primed;

    public Task<bool> PostAsync(string id, string title, string subtitle, string body, CancellationToken cancellationToken)
    {
        if (!_primed)
        {
            _primed = true;
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
