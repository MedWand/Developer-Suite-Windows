using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace SampleWpfApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        foreach (var key in Resources.Keys)
        {
            if (Resources[key] is SolidColorBrush { CanFreeze: true } b)
            {
                b.Freeze();
            }
        }
    }
}

internal static class Settings
{
    public static readonly string AppCompany = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Untitled Company";
    public static readonly string AppProduct = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Unknown Product";
    public static readonly string AppCopyright = DateTime.UtcNow.Year.ToString();
    public static readonly Version AppVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
    public static readonly string AppBuild = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "Unknown Build";
    public static readonly string MwSdkLicense = @"";
    public static readonly string MwSdkPublicKey = @"";
}


