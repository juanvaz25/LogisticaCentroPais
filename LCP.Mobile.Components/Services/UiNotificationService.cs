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

    public List<ToastMessage> ActiveToasts { get; } = new();
    public event Action? OnChange;

    public void Show(string text, string type = "info")
    {
        var toast = new ToastMessage { Text = text, Type = type };
        ActiveToasts.Add(toast);
        OnChange?.Invoke();

        Task.Delay(3500).ContinueWith(_ =>
        {
            ActiveToasts.Remove(toast);
            OnChange?.Invoke();
        });
    }

    public void Success(string text) => Show(text, "success");
    public void Error(string text) => Show(text, "danger");
    public void Warning(string text) => Show(text, "warning");
    public void Info(string text) => Show(text, "info");
}

