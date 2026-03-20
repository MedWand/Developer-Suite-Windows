using System.Windows.Threading;

namespace SampleWpfApp.Core.Extensions;

public static class ControlExtensions
{
    public static void SafeInvoke(this DispatcherObject obj, Action action)
    {
        if (obj.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            obj.Dispatcher.Invoke(action, DispatcherPriority.Background);
        }
    }

    // Optional: async version
    public static void SafeInvokeAsync(this DispatcherObject obj, Action action)
    {
        if (obj.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            obj.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        }
    }
}