using MWSDK.Wpf;
using SampleWpfApp.Interfaces;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MWSDK.NetCore;
using SampleWpfApp.Core.Extensions;
using SampleWpfApp.Views;

namespace SampleWpfApp;

public partial class MainWindow : INotifyPropertyChanged
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
        Mouse.OverrideCursor = Cursors.Wait;
        InitializeComponent();
        _contentRenderedHandler = void (_, _) =>
        {
            try
            {
                ContentRendered -= _contentRenderedHandler!;
                _contentRenderedHandler = null;
                InitializeSafe();
            }
            catch (Exception)
            {
                // ignored
            }
        };

        ContentRendered += _contentRenderedHandler;
        MainFrame.Navigated += MainFrame_Navigated;

        SetNavigation(false, false);
        UpdateStatus("Starting...");

    }

    private EventHandler? _contentRenderedHandler;
    private MedWandController? _medWandController;
    private ThermometerView? _thermometerView;
    private PulseOximeterView? _pulseOximeterView;
    private StethoscopeView? _stethoscopeView;
    private CameraView? _cameraView;
    private EcgView? _ecgView;
    private ISensorView? _currentSensorView;
    
    private string GeneralStatus => $"Device: {_medWandController?.ComPort}/{_medWandController?.VendorId}/{_medWandController?.ProductId} | {_medWandController?.Udi} | {_medWandController?.Generation} v{_medWandController?.FirmwareVersion}";
    
    private void InitializeSafe()
    {
        try
        {
            Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Debug.WriteLine(ex);
            Cleanup();
            Application.Current.Shutdown();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void Initialize()
    {
        ConnectMedWand();
        InitializeMedWand();
        InitializeUserInterface();
    }

    private void ConnectMedWand()
    {
        if (string.IsNullOrEmpty(Settings.MwSdkLicense) || string.IsNullOrEmpty(Settings.MwSdkPublicKey))
            throw new Exception("No valid license information");
        _medWandController ??= new MedWandController();
        _medWandController.LicenseError += _medWandController_LicenseError;
        _medWandController.Construct(Settings.MwSdkLicense, Settings.MwSdkPublicKey);
        if (!_medWandController.IsLicenseValid)
            throw new Exception("No valid license");
        UpdateStatus("Connecting to MedWand.");
        var done = false;
        do
        {
            try
            {
                _medWandController.Connect();
                if (!_medWandController.IsConnected)
                {
                    var resultDialog = MessageBox.Show(
                        "MedWand not found. PLease connect your MedWand and try again.",
                        "MedWand Not Found",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning
                    );
                    if (resultDialog == MessageBoxResult.Cancel) break;
                }
                else
                {
                    done = true;
                }
            }
            catch (Exception outerEx)
            {
                Debug.WriteLine(outerEx);

            }
        } while (!done);

        if (done)
        {
            _medWandController.DeviceError += MedWandController_MedWandDeviceError;
            _medWandController.DeviceStateChanged += MedWandController_DeviceStateChanged;
        }
        else
        {
            _medWandController.LicenseError -= _medWandController_LicenseError;
            _medWandController = null;
            throw new Exception("No MedWand Connected!");
        }
    }

    private void InitializeMedWand()
    {
        UpdateStatus("Initializing MedWand");

        if (!_medWandController?.IsConnected ?? true)
            throw new Exception("MedWand not connected.");

        if (_medWandController == null) return;
        _medWandController.Initialize();

        if (!_medWandController.IsInitialized)
            throw new Exception("MedWand not initialized.");

        _medWandController.ReadingStateChanged += MedWandController_ReadingStateChanged;
        _medWandController.ReadingReceived += MedWandController_ReadingReceived;
    }

    private void InitializeUserInterface()
    {
        PlaceholderText.Visibility =
            MainFrame.Content == null ? Visibility.Visible : Visibility.Collapsed;

        if (_medWandController?.IsInitialized != true)
        {
            SetNavigation(false, true);
            return;
        }

        _thermometerView = new(_medWandController);
        _pulseOximeterView = new(_medWandController);
        _stethoscopeView = new(_medWandController);
        _cameraView = new(_medWandController);
        _ecgView = new(_medWandController);
        _medWandController.Configure(_ecgView.GridContainer);

        DeviceInformation();

        SetNavigation(true, true);
        UpdateStatus(GeneralStatus);
    }

    private void DeviceInformation()
    {
        if (_medWandController is not { IsConnected: true })
            throw new Exception("MedWand not connected.");

        var info = $"""
        Application Information:
        --------------------------------
        Company: {Settings.AppCompany}
        Product: {Settings.AppProduct} (c){Settings.AppCopyright}
        Version: {Settings.AppVersion}
        Build: {Settings.AppBuild}

        Device Information:
        --------------------------------
        ComPort: {_medWandController.ComPort}
        VendorId: {_medWandController.VendorId}
        ProductId: {_medWandController.ProductId}
        DeviceId: {_medWandController.DeviceId}
        UDI: {_medWandController.Udi}
        DeviceState: {_medWandController.DeviceState}
        IsConnected: {_medWandController.IsConnected}
        IsInitialized: {_medWandController.IsInitialized}
        IsBootloaderMode: {_medWandController.IsBootloaderMode()}
        Firmware: {_medWandController.FirmwareVersion}
        Generation: {_medWandController.Generation}
        Camera: {_medWandController.CameraModel}
        
        """;
        PlaceholderInfo = info;
    }

    private void SetNavigation(bool enabled, bool exitEnabled)
    {
        ToolButtonThermometerEnabled = enabled;
        ToolButtonPulseOximeterEnabled = enabled;
        ToolButtonStethoscopeEnabled = enabled;
        ToolButtonCameraEnabled = enabled;
        ToolButtonEcgEnabled = enabled;
        ToolButtonSummaryEnabled = enabled;
        ToolButtonExitEnabled = exitEnabled;
    }

    private void UpdateStatus(string status) => this.SafeInvoke(() => StatusMessage = status);

    private void ShowView(ISensorView? sensorView)
    {
        if (_currentSensorView != null)
        {
            _currentSensorView.Deactivate();
            _currentSensorView.ViewLockStateChanged -= CurrentSensorView_ViewLockStateChanged;
        }

        _currentSensorView = sensorView;

        if (sensorView == null)
        {
            MainFrame.Content = null;
            return;
        }

        MainFrame.Navigate(sensorView);

        sensorView.ViewLockStateChanged += CurrentSensorView_ViewLockStateChanged;
        sensorView.Activate();

        var nav = MainFrame.NavigationService;
        while (nav.CanGoBack)
            nav.RemoveBackEntry();
    }

    private void Cleanup()
    {
        SetNavigation(false, false);
        UpdateStatus("Cleaning up");

        MainFrame.Navigated -= MainFrame_Navigated;

        // Disconnect sensor view
        ShowView(null);

        // Dispose all views
        _thermometerView?.Dispose();
        _pulseOximeterView?.Dispose();
        _stethoscopeView?.Dispose();
        _cameraView?.Dispose();
        _ecgView?.Dispose();

        _thermometerView = null;
        _pulseOximeterView = null;
        _stethoscopeView = null;
        _cameraView = null;
        _ecgView = null;

        if (_medWandController != null)
        {
            _medWandController.StopSensor();

            _medWandController.LicenseError -= _medWandController_LicenseError;
            _medWandController.DeviceError -= MedWandController_MedWandDeviceError;
            _medWandController.DeviceStateChanged -= MedWandController_DeviceStateChanged;
            _medWandController.ReadingStateChanged -= MedWandController_ReadingStateChanged;
            _medWandController.ReadingReceived -= MedWandController_ReadingReceived;

            _medWandController.Dispose();
        }

        _medWandController = null;
    }

    private void Thermometer_Click(object s, RoutedEventArgs e)
    {
        if (_thermometerView != null) ShowView(_thermometerView);
    }

    private void PulseOximeter_Click(object s, RoutedEventArgs e)
    {
        if (_pulseOximeterView != null) ShowView(_pulseOximeterView);
    }

    private void Stethoscope_Click(object s, RoutedEventArgs e)
    {
        if (_stethoscopeView != null) ShowView(_stethoscopeView);
    }

    private void Camera_Click(object s, RoutedEventArgs e)
    {
        if (_cameraView != null) ShowView(_cameraView);
    }

    private void Ecg_Click(object s, RoutedEventArgs e)
    {
        if (_ecgView != null) ShowView(_ecgView);
    }

    private void Exit_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("Are you sure you want to exit?", "Exit Confirmation",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Cleanup();
            UpdateStatus("Shutting down...");
            Application.Current.Shutdown();
        }
    }

    private void MainFrame_Navigated(object s, System.Windows.Navigation.NavigationEventArgs e)
    {
        PlaceholderText.Visibility =
            MainFrame.Content == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CurrentSensorView_ViewLockStateChanged(bool locked) =>
        SetNavigation(true, true);

    private void _medWandController_LicenseError(LicenseState state) =>
        Debug.WriteLine($"LicenseError: {state}");


    private void MedWandController_MedWandDeviceError(MedWandDeviceError deviceError)
    {
        this.SafeInvoke(() =>
        {
            _currentSensorView?.OnDeviceError(deviceError);
        });
    }

    private void MedWandController_DeviceStateChanged(DeviceState deviceState)
    {
        this.SafeInvoke(() =>
        {

        });
    }

    private void MedWandController_ReadingStateChanged(ReadingState readingState)
    {
        this.SafeInvoke(() =>
        {
            _currentSensorView?.OnReadingStateChanged(readingState);
        });
    }

    private void MedWandController_ReadingReceived(MedWandReading reading)
    {
        this.SafeInvoke(() =>
        {
            if (!Enum.TryParse(reading.SensorType, out MedWandSensor sensorType))
            {
                return;
            }

            switch (sensorType)
            {
                case MedWandSensor.Thermometer:
                    _currentSensorView?.OnReadingReceived(reading);
                    break;
                case MedWandSensor.PulseOximeter:
                    _currentSensorView?.OnReadingReceived(reading);
                    break;
                case MedWandSensor.Ecg:
                    _currentSensorView?.OnReadingReceived(reading);
                    break;
                default:
                    break;
            }
        });
    }




    #region Bindings

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _toolButtonThermometerEnabled = true;
    public bool ToolButtonThermometerEnabled
    {
        get => _toolButtonThermometerEnabled;
        set
        {
            if (_toolButtonThermometerEnabled == value) return;
            _toolButtonThermometerEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _toolButtonPulseOximeterEnabled = true;
    public bool ToolButtonPulseOximeterEnabled
    {
        get => _toolButtonPulseOximeterEnabled;
        set
        {
            if (_toolButtonPulseOximeterEnabled == value) return;
            _toolButtonPulseOximeterEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _toolButtonStethoscopeEnabled = true;
    public bool ToolButtonStethoscopeEnabled
    {
        get => _toolButtonStethoscopeEnabled;
        set
        {
            if (_toolButtonStethoscopeEnabled == value) return;
            _toolButtonStethoscopeEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _toolButtonCameraEnabled = true;
    public bool ToolButtonCameraEnabled
    {
        get => _toolButtonCameraEnabled;
        set
        {
            if (_toolButtonCameraEnabled == value) return;
            _toolButtonCameraEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _toolButtonEcgEnabled = true;
    public bool ToolButtonEcgEnabled
    {
        get => _toolButtonEcgEnabled;
        set
        {
            if (_toolButtonEcgEnabled == value) return;
            _toolButtonEcgEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _toolButtonSummaryEnabled = true;
    public bool ToolButtonSummaryEnabled
    {
        get => _toolButtonSummaryEnabled;
        set
        {
            if (_toolButtonSummaryEnabled == value) return;
            _toolButtonSummaryEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _toolButtonExitEnabled = true;
    public bool ToolButtonExitEnabled
    {
        get => _toolButtonExitEnabled;
        set
        {
            if (_toolButtonExitEnabled == value) return;
            _toolButtonExitEnabled = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage = "Starting";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private string _placeholderInfo = "Starting...";
    public string PlaceholderInfo
    {
        get => _placeholderInfo;
        set
        {
            if (_placeholderInfo != value)
            {
                _placeholderInfo = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

}
