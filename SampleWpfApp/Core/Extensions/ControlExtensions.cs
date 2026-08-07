using System.Windows.Threading;

namespace SampleWpfApp.Core.Extensions;

public static class ControlExtensions
{
    /// <summary>
    /// Executes the specified <paramref name="action"/> on the thread associated with the 
    /// <see cref="DispatcherObject"/>. If the calling thread is not the UI thread, the action
    /// is invoked on the UI thread using the dispatcher.
    /// </summary>
    /// <param name="obj">The <see cref="DispatcherObject"/> whose dispatcher is used to invoke the action.</param>
    /// <param name="action">The <see cref="Action"/> to be executed.</param>
    /// <remarks>
    /// This method ensures thread-safe execution of actions that interact with UI elements.
    /// Use this method to update UI components from a non-UI thread.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="obj"/> or <paramref name="action"/> is <c>null</c>.
    /// </exception>
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

    /// <summary>
    /// Executes the specified <paramref name="action"/> asynchronously on the thread associated 
    /// with the <see cref="DispatcherObject"/>. If the calling thread is not the UI thread, the action
    /// is invoked asynchronously on the UI thread using the dispatcher.
    /// </summary>
    /// <param name="obj">The <see cref="DispatcherObject"/> whose dispatcher is used to invoke the action.</param>
    /// <param name="action">The <see cref="Action"/> to be executed asynchronously.</param>
    /// <remarks>
    /// This method ensures thread-safe execution of actions that interact with UI elements.
    /// Use this method to update UI components from a non-UI thread without blocking the calling thread.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="obj"/> or <paramref name="action"/> is <c>null</c>.
    /// </exception>
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