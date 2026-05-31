namespace MyBlazorSite.Data;

public class DashboardStatsNotifier
{
    public event Func<Task>? OnChange;

    public async Task NotifyChangedAsync()
    {
        if (OnChange is null)
            return;

        var handlers = OnChange.GetInvocationList();

        foreach (var handler in handlers)
        {
            if (handler is Func<Task> asyncHandler)
            {
                await asyncHandler();
            }
        }
    }
}