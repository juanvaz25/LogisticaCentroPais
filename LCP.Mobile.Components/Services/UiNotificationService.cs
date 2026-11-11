namespace LCP.Mobile.Components.Services;

public class UiNotificationService
{
    public class ToastMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = "info"; // success, danger, warning, info
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    private readonly List<ToastMessage> _toasts = new();
    private readonly object _lock = new();

    public event Action? OnChange;

    public List<ToastMessage> GetActiveToasts()
    {
        lock (_lock)
        {
            return _toasts.ToList();
        }
    }

    public void Show(string text, string type = "info", int durationMs = 3500)
    {
        var toast = new ToastMessage { Text = text, Type = type };
        lock (_lock)
        {
            _toasts.Add(toast);
        }
        OnChange?.Invoke();

        _ = Task.Run(async () =>
        {
            await Task.Delay(durationMs);
            Dismiss(toast.Id);
        });
    }

    public void Dismiss(string id)
    {
        bool removed = false;
        lock (_lock)
        {
            var item = _toasts.FirstOrDefault(t => t.Id == id);
            if (item != null)
            {
                _toasts.Remove(item);
                removed = true;
            }
        }

        if (removed)
        {
            OnChange?.Invoke();
        }
    }

    public void Success(string text) => Show(text, "success", 3500);
    public void Error(string text) => Show(text, "danger", 4500);
    public void Warning(string text) => Show(text, "warning", 4500);
    public void Info(string text) => Show(text, "info", 3500);
}
