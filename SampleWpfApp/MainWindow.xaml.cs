using Microsoft.Extensions.Logging;
using MWSDK.NetCore;
using MWSDK.NetCore.Core;
using MWSDK.Wpf;
using SampleWpfApp.Core.Extensions;
using SampleWpfApp.Interfaces;
using SampleWpfApp.Views;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace SampleWpfApp;

/// <summary>
/// Represents the main window of the SampleWpfApp application.<br/><br/>
/// This copy of the MedWand Software Libraries (MSL) is licensed to You as the end user or to
/// Your employer or another third party authorized to permit under the TERMS AND CONDITIONS OF THE MSL LICENSE AGREEMENT.
/// Firmware updates may be required to correct performance problems and will be provided under the MSL Agreement
/// where deemed applicable by MedWand. No license is granted to you for redistribution of MedWand Device Firmware.
/// You expressly agree to make Device Firmware Upgrade calls, using the provided DECL(s) libraries whenever you are notified
/// of a new firmware upgrade, and in any event, not less than every 90 days, and receive firmware updates
/// for the MedWand hardware device within your software workflow and/or UI. Failure to update firmware when notified
/// of a firmware update may affect the availability of features and the reliability of the MedWand and its related software.
/// See MSL agreement for additional details.
/// </summary>
/// <remarks>
/// This class serves as the entry point for the application's user interface.
/// It implements <see cref="System.ComponentModel.INotifyPropertyChanged"/> to support property change notifications.
/// </remarks>
public partial class MainWindow : INotifyPropertyChanged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <remarks>
    /// This constructor sets up the initial state of the <see cref="MainWindow"/> class, 
    /// including initializing components, configuring logging, and setting up event handlers 
    /// for navigation and content rendering.
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
        Mouse.OverrideCursor = Cursors.Wait;
        InitializeComponent();
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddDebug();
        });
        _logger = _loggerFactory.CreateLogger<MainWindow>();
        _logger.LogInformation("MainWindow constructed.");
        _contentRenderedHandler = async void (_, _) =>
        {
            try
            {
                ContentRendered -= _contentRenderedHandler!;
                _contentRenderedHandler = null;
                await InitializeSafe();
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
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainWindow> _logger;
    private const string MedwandStorageId = "MedWandFirmwareCheckDate";
    private DateTime _lastFirmwareCheckDate = DateTime.UtcNow;
    private bool _checkFirmwareVersion;
    private string _firmwareProgressStep = "";
    private int _firmwareProgressValue;

    private string GeneralStatus => $"Device: {_medWandController?.ComPort}/{_medWandController?.VendorId}/{_medWandController?.ProductId} | {_medWandController?.Udi} | {_medWandController?.Generation} v{_medWandController?.FirmwareVersion}";
    
    /// <summary>
    /// Asynchronously initializes the application in a safe manner, handling potential exceptions.
    /// </summary>
    /// <remarks>
    /// This method ensures that the initialization process is executed safely. If an exception occurs during initialization,
    /// it displays an error message to the user, logs the exception, performs cleanup, and shuts down the application.
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="Exception">Thrown if an error occurs during the initialization process.</exception>
    private async Task InitializeSafe()
    {
        try
        {
            await Initialize();
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

    /// <summary>
    /// Asynchronously initializes the application by performing the following tasks:
    /// - Retrieves or sets the last firmware check date from a storage mechanism.
    /// - Establishes a connection with the MedWand device.
    /// - Initializes the MedWand device.
    /// - Configures the user interface.
    /// </summary>
    /// <remarks>
    /// The method ensures that the application is properly set up to interact with the MedWand device
    /// and provides a consistent user experience. It uses an environment variable to persist the last
    /// firmware check date.
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task Initialize()
    {
        // Get last firmware check date from your preferred storage mechanism (see you MedWand contract for details)
        var value = Environment.GetEnvironmentVariable(MedwandStorageId);
        if (string.IsNullOrWhiteSpace(value))
        {
            _lastFirmwareCheckDate = DateTime.UtcNow.AddDays(-15);
            Environment.SetEnvironmentVariable(MedwandStorageId, _lastFirmwareCheckDate.ToString("O"), EnvironmentVariableTarget.User);
        }
        else
        {
            _lastFirmwareCheckDate = DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        await ConnectMedWand();
        InitializeMedWand();
        InitializeUserInterface();
    }

    /// <summary>
    /// Disposes the current instance of <see cref="MedWandController"/> if it exists.
    /// </summary>
    /// <remarks>
    /// This method stops the sensor, unsubscribes from all event handlers, disposes the controller,
    /// and sets the reference to null.
    /// </remarks>
    private void DisposeCurrentMedWandController()
    {
        if (_medWandController == null) return;

        _medWandController.StopSensor();
        _medWandController.OnLicenseError -= MedWandController_LicenseError;
        _medWandController.OnDeviceError -= MedWandController_MedWandDeviceError;
        _medWandController.OnDeviceStateChanged -= MedWandController_DeviceStateChanged;
        _medWandController.OnReadingStateChanged -= MedWandController_ReadingStateChanged;
        _medWandController.OnReadingReceived -= MedWandController_ReadingReceived;

        _medWandController.Dispose();
        _medWandController = null;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="MedWandController"/> and initializes it with the license and public key
    /// from the application settings. If a controller already exists, it disposes of the current instance before creating a new one.
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown when the license information is missing or invalid.
    /// </exception>
    /// <remarks>
    /// This method ensures that the <see cref="MedWandController"/> is properly initialized and ready for use.
    /// It also subscribes to the <see cref="MedWandController.OnLicenseError"/> event to handle license errors.
    /// </remarks>
    private void CreateNewMedWandController()
    {
        DisposeCurrentMedWandController();

        if (string.IsNullOrEmpty(Settings.MwSdkLicense) || string.IsNullOrEmpty(Settings.MwSdkPublicKey))
            throw new Exception("No valid license information");

        _medWandController ??= new MedWandController();
        _medWandController.OnLicenseError += MedWandController_LicenseError;
        _medWandController.Construct(Settings.MwSdkLicense, Settings.MwSdkPublicKey);

        if (!_medWandController.IsLicenseValid)
            throw new Exception("No valid license");
    }

    /// <summary>
    /// Establishes a connection to the MedWand device and handles its initialization process.
    /// </summary>
    /// <remarks>
    /// This method attempts to connect to the MedWand device and ensures that the firmware is up-to-date.
    /// If a firmware update is required, the application will shut down after cleanup.
    /// Event handlers for device errors and state changes are registered upon successful connection.
    /// </remarks>
    /// <exception cref="Exception">Thrown if no MedWand device is connected.</exception>
    /// <exception cref="MedWandFirmwareUpdateRequiredException">
    /// Thrown when a firmware update is required for the connected MedWand device.
    /// </exception>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task ConnectMedWand()
    {
        UpdateStatus("Connecting to MedWand.");

        // Check for firmware update every [n] days (see you MedWand contract for details)
        // const int n = 15;
        const int n = -1; // Set to -1 to force an update check for testing
        _checkFirmwareVersion = (DateTime.UtcNow - _lastFirmwareCheckDate).TotalDays > n;

        var done = false;
        do
        {
            try
            {
                CreateNewMedWandController();
                _medWandController?.Connect();
                if (_checkFirmwareVersion)
                {
                    _medWandController?.CheckFirmwareVersion();
                    // Force an update regardless of _checkFirmwareVersion value. Can be used as an override.
                    // throw new MedWandFirmwareUpdateRequiredException(new Version(0, 0, 0, 0), new Version(1, 0, 0, 0), "Force firmware update");
                }
                if (_medWandController is not { IsConnected: true })
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
            catch (MedWandFirmwareUpdateRequiredException updateEx)
            {
                DisposeCurrentMedWandController();
                done = !await MedWandFirmwareProcess(updateEx.CurrentVersion, updateEx.RequiredVersion);
            }
            catch (Exception outerEx)
            {
                Debug.WriteLine(outerEx);

            }
        } while (!done);

        switch (_medWandController?.DeviceState)
        {
            case DeviceState.Connected:
                _medWandController.OnDeviceError += MedWandController_MedWandDeviceError;
                _medWandController.OnDeviceStateChanged += MedWandController_DeviceStateChanged;
                break;
            case DeviceState.FirmwareUpdateRequired:
                Cleanup();
                UpdateStatus("Shutting down...");
                Application.Current.Shutdown();
                break;
            default:
                DisposeCurrentMedWandController();
                throw new Exception("No MedWand Connected!");
        }
    }

    /// <summary>
    /// Initializes the MedWand device by setting up its state and event handlers.
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown if the MedWand device is not connected or fails to initialize.
    /// </exception>
    /// <remarks>
    /// This method ensures that the MedWand device is properly initialized and ready for use. 
    /// It verifies the connection status, initializes the device, and subscribes to relevant events 
    /// for handling state changes and received readings.
    /// </remarks>
    private void InitializeMedWand()
    {
        UpdateStatus("Initializing MedWand");

        if (!_medWandController?.IsConnected ?? true)
            throw new Exception("MedWand not connected.");

        if (_medWandController == null) return;
        _medWandController.Initialize();

        if (!_medWandController.IsInitialized)
            throw new Exception("MedWand not initialized.");

        _medWandController.OnReadingStateChanged += MedWandController_ReadingStateChanged;
        _medWandController.OnReadingReceived += MedWandController_ReadingReceived;
    }

    /// <summary>
    /// Initializes the user interface components of the application.
    /// </summary>
    /// <remarks>
    /// This method configures the visibility of placeholder text based on the content of the main frame.
    /// It also initializes various sensor views (e.g., thermometer, pulse oximeter, stethoscope, camera, ECG)
    /// and configures the MedWand controller with the necessary settings.
    /// Additionally, it updates the device information, navigation state, and status message.
    /// </remarks>
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

    /// <summary>
    /// Retrieves and formats detailed information about the application and the connected MedWand device.
    /// </summary>
    /// <remarks>
    /// This method gathers application metadata such as company, product, version, and build information,
    /// as well as device-specific details including connection status, firmware version, and hardware identifiers.
    /// The formatted information is assigned to the <see cref="PlaceholderInfo"/> property.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown when the MedWand device is not connected.
    /// </exception>
    private void DeviceInformation()
    {
        if (_medWandController is not { IsConnected: true })
            throw new Exception("MedWand not connected.");

        var info = $$"""
        Application Information:
        --------------------------------
        Company: {{Settings.AppCompany}}
        Product: {{Settings.AppProduct}} (c){{Settings.AppCopyright}}
        Version: {{Settings.AppVersion}}
        Build: {{Settings.AppBuild}}

        Device Information:
        --------------------------------
        ComPort: {{_medWandController.ComPort}}
        VendorId: {{_medWandController.VendorId}}
        ProductId: {{_medWandController.ProductId}}
        DeviceId: {{_medWandController.DeviceId}}
        UDI: {{_medWandController.Udi}}
        DeviceState: {{_medWandController.DeviceState}}
        IsConnected: {{_medWandController.IsConnected}}
        IsInitialized: {{_medWandController.IsInitialized}}
        IsBootloaderMode: {{_medWandController.IsBootloaderMode()}}
        InstalledFirmware: {{_medWandController.FirmwareVersion}}
        LastFirmwareCheckDate: ({{_checkFirmwareVersion}}) {{_lastFirmwareCheckDate}}
        Generation: {{_medWandController.Generation}}
        Camera: {{_medWandController.CameraModel}}
        
        """;
        PlaceholderInfo = info;
    }

    /// <summary>
    /// Handles the firmware update process for the MedWand device.
    /// </summary>
    /// <param name="currentVersion">The current firmware version of the MedWand device.</param>
    /// <param name="requiredVersion">The required firmware version to which the MedWand device needs to be updated.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result is <c>true</c> if the firmware update process completes successfully; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method initiates the firmware update process, displays user prompts, and handles errors or progress updates.
    /// It ensures proper cleanup of resources and updates the status message upon completion.
    /// </remarks>
    /// <exception cref="Exception">Thrown if an error occurs during the firmware update process.</exception>
    private async Task<bool> MedWandFirmwareProcess(Version currentVersion, Version requiredVersion)
    {
        UpdateStatus("Firmware Update Statring...");
        var cancellation = new CancellationTokenSource();
        FirmwareController medwandFirmwareController = new(cancellation.Token, _loggerFactory);
        medwandFirmwareController.FirmwareStateChanged += MedwandFirmwareStateChanged;
        medwandFirmwareController.FirmwareProgressChanged += MedwandFirmwareProgressChanged;
        medwandFirmwareController.FirmwareError += MedwandFirmwareError;
        try
        {
            var resultDialog = MessageBox.Show(
                $"Firmware update required from {currentVersion} to {requiredVersion}. Do not disconnect your MedWand.",
                "MedWand Firmware Update",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning
            );
            if (resultDialog == MessageBoxResult.Cancel) return false;
            var result = await medwandFirmwareController.StartAsync();
            if (result)
            {
                // Put last firmware check date into your preferred storage mechanism so you can check again in [n] days (see you MedWand contract for details)
                _lastFirmwareCheckDate = DateTime.UtcNow;
                Environment.SetEnvironmentVariable(MedwandStorageId, _lastFirmwareCheckDate.ToString("O"), EnvironmentVariableTarget.User);
            }
            MessageBox.Show(
                $"Firmware update result: {result}",
                "MedWand Firmware Update",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            return true;
        }
        catch (Exception outerEx)
        {
            _logger.LogError(outerEx, "MedWandFirmwareProcess failed.");
            MessageBox.Show(
                $"ERROR: {outerEx.Message}",
                "MedWand Firmware Update",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return true;
        }
        finally
        {
            medwandFirmwareController.FirmwareStateChanged -= MedwandFirmwareStateChanged;
            medwandFirmwareController.FirmwareProgressChanged -= MedwandFirmwareProgressChanged;
            medwandFirmwareController.FirmwareError -= MedwandFirmwareError;
            await medwandFirmwareController.DisposeAsync();
            UpdateStatus("Firmware Update Complete.");
        }
    }

    /// <summary>
    /// Handles the firmware error event for the Medwand device.
    /// </summary>
    /// <param name="sender">
    /// The source of the event, which can be <c>null</c>.
    /// </param>
    /// <param name="e">
    /// The <see cref="Exception"/> instance containing details about the firmware error.
    /// </param>
    /// <remarks>
    /// This method logs the error details and updates the status message to display the error message.
    /// </remarks>
    private void MedwandFirmwareError(object? sender, Exception e)
    {
        _logger.LogError(e, "MedwandFirmwareError()");
        UpdateStatus(e.Message);
    }

    /// <summary>
    /// Handles the progress change event during the MedWand firmware update process.
    /// </summary>
    /// <param name="sender">The source of the event, typically the firmware controller.</param>
    /// <param name="e">The current progress value of the firmware update, represented as a percentage.</param>
    /// <remarks>
    /// This method updates the progress value, logs the progress, and updates the status message displayed in the UI.
    /// </remarks>
    private void MedwandFirmwareProgressChanged(object? sender, int e)
    {
        if (e == _firmwareProgressValue) return;
        _firmwareProgressValue = e;
        _logger.LogDebug("Progress -> {FirmwareProgressStep}({FirmwareProgressValue}%)", _firmwareProgressStep, _firmwareProgressValue);
        UpdateStatus($"{_firmwareProgressStep}({_firmwareProgressValue}%)");
    }

    /// <summary>
    /// Handles the event triggered when the firmware state of the MedWand device changes.
    /// </summary>
    /// <param name="sender">The source of the event, typically the <see cref="FirmwareController"/> instance.</param>
    /// <param name="e">The updated firmware state represented as an <see cref="UpdaterState"/>.</param>
    /// <remarks>
    /// This method updates the firmware progress step, logs the new state, and updates the user interface
    /// with the current firmware update status.
    /// </remarks>
    private void MedwandFirmwareStateChanged(object? sender, UpdaterState e)
    {
        _firmwareProgressStep = e.ToString();
        _logger.LogDebug("Status -> {FirmwareProgressStep}", _firmwareProgressStep);
        UpdateStatus($"MedwandUpdateStatusChanged => {e}");
    }

    /// <summary>
    /// Configures the navigation state of the application's toolbar buttons.
    /// </summary>
    /// <param name="enabled">A boolean value indicating whether the main navigation buttons (e.g., Thermometer, Pulse Oximeter, etc.) should be enabled.</param>
    /// <param name="exitEnabled">A boolean value indicating whether the Exit button should be enabled.</param>
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

    /// <summary>
    /// Updates the current status message displayed in the application.
    /// </summary>
    /// <param name="status">The status message to be displayed.</param>
    /// <remarks>
    /// This method ensures thread-safe updates to the <see cref="StatusMessage"/> property
    /// by invoking the update on the UI thread.
    /// </remarks>
    private void UpdateStatus(string status) => this.SafeInvoke(() => StatusMessage = status);

    /// <summary>
    /// Displays the specified sensor view in the main frame and manages its lifecycle.
    /// </summary>
    /// <param name="sensorView">
    /// The <see cref="ISensorView"/> instance to display. Pass <c>null</c> to clear the current view.
    /// </param>
    /// <remarks>
    /// If a view is already active, it will be deactivated and its event handlers will be unsubscribed 
    /// before the new view is displayed. The navigation history of the main frame is also cleared.
    /// </remarks>
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

    /// <summary>
    /// Cleans up resources and resets the application state.
    /// </summary>
    /// <remarks>
    /// This method performs the following actions:
    /// - Disables navigation and updates the status message.
    /// - Unsubscribes from the <see cref="MainFrame"/> navigation event.
    /// - Disconnects and disposes of all sensor views.
    /// - Sets all sensor view references to <c>null</c>.
    /// - Disposes the current MedWand controller.
    /// </remarks>
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

        DisposeCurrentMedWandController();
    }

    /// <summary>
    /// Handles the click event for the thermometer button in the user interface.
    /// </summary>
    /// <param name="s">The source of the event, typically the button that was clicked.</param>
    /// <param name="e">The event data associated with the click event.</param>
    /// <remarks>
    /// This method displays the thermometer view if it has been initialized.
    /// </remarks>
    private void Thermometer_Click(object s, RoutedEventArgs e)
    {
        if (_thermometerView != null) ShowView(_thermometerView);
    }

    /// <summary>
    /// Handles the click event for the Pulse Oximeter button in the user interface.
    /// </summary>
    /// <param name="s">The source of the event, typically the button that was clicked.</param>
    /// <param name="e">The event data associated with the click event.</param>
    /// <remarks>
    /// This method checks if the <see cref="_pulseOximeterView"/> is not null and, if so, 
    /// invokes the <see cref="ShowView(ISensorView?)"/> method to display the Pulse Oximeter view.
    /// </remarks>
    private void PulseOximeter_Click(object s, RoutedEventArgs e)
    {
        if (_pulseOximeterView != null) ShowView(_pulseOximeterView);
    }

    /// <summary>
    /// Handles the click event for the Stethoscope button in the user interface.
    /// </summary>
    /// <param name="s">The source of the event, typically the button that was clicked.</param>
    /// <param name="e">The event data associated with the click event.</param>
    /// <remarks>
    /// This method checks if the <see cref="_stethoscopeView"/> is not null and, if so, invokes the <see cref="ShowView"/> method
    /// to display the stethoscope view.
    /// </remarks>
    private void Stethoscope_Click(object s, RoutedEventArgs e)
    {
        if (_stethoscopeView != null) ShowView(_stethoscopeView);
    }

    /// <summary>
    /// Handles the click event for the Camera button.
    /// </summary>
    /// <param name="s">The source of the event, typically the button that was clicked.</param>
    /// <param name="e">The event data associated with the click event.</param>
    /// <remarks>
    /// This method checks if the <see cref="_cameraView"/> is not null and, if so, displays the camera view
    /// by invoking the <see cref="ShowView(ISensorView?)"/> method.
    /// </remarks>
    private void Camera_Click(object s, RoutedEventArgs e)
    {
        if (_cameraView != null) ShowView(_cameraView);
    }

    /// <summary>
    /// Handles the Click event for the ECG button in the user interface.
    /// </summary>
    /// <param name="s">The source of the event, typically the button that was clicked.</param>
    /// <param name="e">The event data associated with the Click event.</param>
    /// <remarks>
    /// This method checks if the <see cref="_ecgView"/> is not null and, if so, displays the ECG view
    /// by invoking the <see cref="ShowView(ISensorView?)"/> method.
    /// </remarks>
    private void Ecg_Click(object s, RoutedEventArgs e)
    {
        if (_ecgView != null) ShowView(_ecgView);
    }

    /// <summary>
    /// Handles the click event for the Exit button in the application's user interface.
    /// </summary>
    /// <param name="s">The source of the event, typically the button that was clicked.</param>
    /// <param name="e">The event data associated with the click event.</param>
    /// <remarks>
    /// This method displays a confirmation dialog to the user. If the user confirms the action, 
    /// it performs necessary cleanup operations, updates the application status, and shuts down the application.
    /// </remarks>
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

    /// <summary>
    /// Handles the <see cref="System.Windows.Controls.Frame.Navigated"/> event of the <c>MainFrame</c>.
    /// </summary>
    /// <param name="s">The source of the event.</param>
    /// <param name="e">The <see cref="System.Windows.Navigation.NavigationEventArgs"/> instance containing the event data.</param>
    /// <remarks>
    /// This method updates the visibility of the <c>PlaceholderText</c> element based on whether the <c>MainFrame</c>
    /// contains any content. If the content is <c>null</c>, the placeholder text is made visible; otherwise, it is hidden.
    /// </remarks>
    private void MainFrame_Navigated(object s, System.Windows.Navigation.NavigationEventArgs e)
    {
        PlaceholderText.Visibility =
            MainFrame.Content == null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Handles the <see cref="ISensorView.ViewLockStateChanged"/> event for the current sensor view.
    /// </summary>
    /// <param name="locked">
    /// A boolean value indicating whether the view is locked (<c>true</c>) or unlocked (<c>false</c>).
    /// </param>
    /// <remarks>
    /// This method updates the navigation state by enabling or disabling navigation and exit functionality
    /// based on the current lock state of the sensor view.
    /// </remarks>
    private void CurrentSensorView_ViewLockStateChanged(bool locked) =>
        SetNavigation(true, true);

    /// <summary>
    /// Handles the license error event triggered by the <see cref="MedWandController"/>.
    /// </summary>
    /// <param name="state">The current state of the license, represented by the <see cref="LicenseState"/> enumeration.</param>
    /// <remarks>
    /// This method is invoked when the <see cref="MedWandController"/> detects an issue with the license.
    /// It logs the license error details for debugging purposes.
    /// </remarks>
    private void MedWandController_LicenseError(LicenseState state) =>
        Debug.WriteLine($"LicenseError: {state}");

    /// <summary>
    /// Handles the MedWand device error event.
    /// </summary>
    /// <param name="deviceError">The <see cref="MedWandDeviceError"/> instance containing details about the device error.</param>
    /// <remarks>
    /// This method is invoked when the MedWand device reports an error. It ensures that the error is handled safely
    /// on the UI thread and delegates the error handling to the current sensor view, if available.
    /// </remarks>
    private void MedWandController_MedWandDeviceError(MedWandDeviceError deviceError)
    {
        this.SafeInvoke(() =>
        {
            _currentSensorView?.OnDeviceError(deviceError);
        });
    }

    /// <summary>
    /// Handles the state change of the MedWand device.
    /// </summary>
    /// <param name="deviceState">The new state of the MedWand device.</param>
    /// <remarks>
    /// This method is invoked when the state of the MedWand device changes. 
    /// It ensures that the state change handling is executed on the UI thread using <see cref="ControlExtensions.SafeInvoke"/>.
    /// </remarks>
    private void MedWandController_DeviceStateChanged(DeviceState deviceState)
    {
        this.SafeInvoke(() =>
        {

        });
    }

    /// <summary>
    /// Handles the event triggered when the reading state of the MedWand controller changes.
    /// </summary>
    /// <param name="readingState">The new reading state of the MedWand controller.</param>
    /// <remarks>
    /// This method ensures thread-safe invocation of the <see cref="_currentSensorView"/> 
    /// to handle the updated reading state.
    /// </remarks>
    private void MedWandController_ReadingStateChanged(ReadingState readingState)
    {
        this.SafeInvoke(() =>
        {
            _currentSensorView?.OnReadingStateChanged(readingState);
        });
    }

    /// <summary>
    /// Handles the event triggered when a new reading is received from the MedWand device.
    /// </summary>
    /// <param name="reading">The <see cref="MedWandReading"/> object containing the data from the sensor.</param>
    /// <remarks>
    /// This method processes the received reading based on the sensor type and updates the corresponding sensor view.
    /// Supported sensor types include:
    /// <list type="bullet">
    /// <item><description><see cref="MedWandSensor.Thermometer"/></description></item>
    /// <item><description><see cref="MedWandSensor.PulseOximeter"/></description></item>
    /// <item><description><see cref="MedWandSensor.Ecg"/></description></item>
    /// </list>
    /// </remarks>
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
