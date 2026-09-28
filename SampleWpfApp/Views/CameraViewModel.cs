using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Core;
using SampleWpfApp.Core.Extensions;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using static MWSDK.NetCore.Internal.CameraHelper;

namespace SampleWpfApp.Views;

/// <summary>
/// Represents the view model for managing camera-related functionalities in the WPF application.
/// </summary>
/// <remarks>
/// This class provides properties, commands, and events to control and interact with the camera,
/// including camera modes, LED intensity, focus settings, and user interactions such as key presses.
/// It implements <see cref="System.ComponentModel.INotifyPropertyChanged"/> to notify changes in property values
/// and <see cref="System.IDisposable"/> to release resources when no longer needed.
/// </remarks>
public sealed class CameraViewModel : INotifyPropertyChanged, IDisposable
{
    public MedWandSensor MedWandSensor => MedWandSensor.Otoscope;
    public CameraModes CameraMode => _medWandController.CameraMode;
    public event Action<bool>? ViewLockStateChanged;
    public event Action<CameraModes>? CameraModeChanged;

    private readonly MedWandController _medWandController;
    private readonly Action<bool> _setLocked;
    private DispatcherTimer? _controlTimer;
    private readonly Image _videoPreview;
    private bool _isActivated;
    private double _focusValue = 0;
    private int _framesCaptured;
    private bool _controlTimerTickDown = true;
    private bool _controlTimerMinCoolDown;
    private int _controlTimerTickCounter;
    private int _controlTimerTickSeconds;
    private string _readingState = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraViewModel"/> class.
    /// </summary>
    /// <param name="medWandController">
    /// The <see cref="MedWandController"/> instance used to manage camera operations and settings.
    /// </param>
    /// <param name="setLocked">
    /// An <see cref="Action{T}"/> delegate to handle the locked state of the view.
    /// </param>
    /// <param name="videoPreview">
    /// The <see cref="Image"/> control used to display the video preview from the camera.
    /// </param>
    /// <remarks>
    /// This constructor sets up the camera view model by associating it with the specified
    /// <paramref name="medWandController"/> and <paramref name="videoPreview"/>.
    /// It also initializes the LED intensity to the maximum value supported by the controller.
    /// </remarks>
    public CameraViewModel(MedWandController medWandController, Action<bool> setLocked, Image videoPreview)
    {
        _medWandController = medWandController;
        _setLocked = setLocked;
        _videoPreview = videoPreview;
        LedIntensityMax = _medWandController.CameraLedIntensityMax;
    }

    /// <summary>
    /// Activates the camera functionality by initializing necessary components and settings.
    /// </summary>
    /// <remarks>
    /// This method ensures that the camera is properly initialized and ready for operation. 
    /// It sets up event handlers, initializes timers if required, and updates the internal state.
    /// </remarks>
    internal void Activate()
    {
        if (!_isActivated)
        {
            _isActivated = true;
            _controlTimerTickCounter = _medWandController.CameraOnTimeMax;
            _controlTimerTickSeconds = _medWandController.CameraOnTimeMax;
            LedIntensityMax = _medWandController.CameraLedIntensityMax;
            if (_medWandController.Camera != null)
            {
                _medWandController.OnLedIntensityChanged += MedWandController_LedIntensityChanged;
                _medWandController.Camera.RecordedFrameReady += Camera_RecordedFrameReady;
                if (_medWandController.CameraHasOnTimer)
                {
                    _controlTimer = new DispatcherTimer(DispatcherPriority.Send)
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    _controlTimer.Tick += OnControlTimerTick;
                    _controlTimerTickCounter = _medWandController.CameraOnTimeMax;
                    _controlTimerTickSeconds = _medWandController.CameraOnTimeMax;
                }
            }
        }
        SetAction(ActionState.Idle);
        UpdateStatus();
    }

    /// <summary>
    /// Deactivates the camera by setting its mode to <see cref="CameraModes.Off"/>.
    /// </summary>
    /// <remarks>
    /// This method is intended to stop the camera's operation and release any associated resources.
    /// It is typically called when the camera is no longer needed or the view is being disposed.
    /// </remarks>
    internal void Deactivate()
    {
        SetCameraMode(CameraModes.Off);
    }

    /// <summary>
    /// Handles key press events to control camera movement, zoom, and reset actions.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <returns>
    /// <see langword="true"/> if the key press was handled and resulted in a camera action; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Supported keys include:
    /// <list type="bullet">
    /// <item><description><see cref="Key.Left"/> and <see cref="Key.Right"/> for horizontal movement.</description></item>
    /// <item><description><see cref="Key.Up"/> and <see cref="Key.Down"/> for vertical movement.</description></item>
    /// <item><description><see cref="Key.PageUp"/> and <see cref="Key.PageDown"/> for zooming in and out.</description></item>
    /// <item><description><see cref="Key.Enter"/> to reset the camera to its default state.</description></item>
    /// </list>
    /// </remarks>
    internal bool KeyDown(Key key)
    {
        switch (key)
        {
            case Key.A:
                _medWandController.CameraSetFocusMode(FocusModes.Auto, true);
                _focusValue = 0;
                _medWandController.CameraSetFocusValue(_focusValue);
                break;
            case Key.M:
                _medWandController.CameraSetFocusMode(FocusModes.Manual, true);
                _focusValue = 1;
                _medWandController.CameraSetFocusValue(_focusValue);
                break;
            case Key.NumPad0:
                if (_medWandController.CameraFocusMode != FocusModes.Manual) return false;
                _focusValue--;
                if (_focusValue < 1) _focusValue = 1;
                _medWandController.CameraSetFocusValue(_focusValue);
                break;
            case Key.NumPad1:
                if (_medWandController.CameraFocusMode != FocusModes.Manual) return false;
                _focusValue++;
                _medWandController.CameraSetFocusValue(_focusValue);
                break;
            case Key.Left:
                _medWandController.CameraMove(-1, null);
                break;
            case Key.Right:
                _medWandController.CameraMove(1, null);
                break;
            case Key.Up:
                _medWandController.CameraMove(null, -1);
                break;
            case Key.Down:
                _medWandController.CameraMove(null, 1);
                break;
            case Key.PageUp:
                _medWandController.CameraRadius(1);
                break;
            case Key.PageDown:
                _medWandController.CameraRadius(-1);
                break;
            case Key.Add:
                _medWandController.CameraZoom(1);
                break;
            case Key.Subtract:
                _medWandController.CameraZoom(-1);
                break;
            case Key.Delete:
                _medWandController.CameraReset();
                break;
            default:
                return false;
        }
        UpdateStatus();
        return true;
    }

    /// <summary>
    /// Sets the camera mode to the specified value and updates the associated state and UI elements.
    /// </summary>
    /// <param name="cameraMode">The <see cref="CameraModes"/> value to set the camera to.</param>
    /// <remarks>
    /// This method adjusts the camera mode, updates the visibility of video overlays, and triggers the 
    /// <see cref="CameraModeChanged"/> event if the mode changes. It also manages the control timer 
    /// and updates the application status.
    /// </remarks>
    internal void SetCameraMode(CameraModes cameraMode)
    {
        if (cameraMode == _medWandController.CameraMode)
        {
            return;
        }

        Mouse.OverrideCursor = Cursors.Wait;
        _setLocked(true);

        _medWandController.SetCameraMode(_videoPreview, cameraMode);

        switch (_medWandController.CameraMode)
        {
            case CameraModes.Off:
                _controlTimerTickDown = false;
                VideoOverlayVisible = Visibility.Visible;
                break;
            case CameraModes.Dermatoscope:
                _controlTimerTickDown = true;
                VideoOverlayVisible = Visibility.Hidden;
                break;
            case CameraModes.Otoscope:
                _controlTimerTickDown = true;
                VideoOverlayVisible = Visibility.Hidden;
                break;
        }
        
        CameraModeChanged?.Invoke(_medWandController.CameraMode);

        if (_medWandController.CameraHasOnTimer && _controlTimer is { IsEnabled: false })
        {
            _controlTimerTickCounter = _medWandController.CameraOnTimeMax;
            _controlTimerTickSeconds = _medWandController.CameraOnTimeMax;
            _controlTimer.Start();
        }

        UpdateStatus();

        _setLocked(false);
        Mouse.OverrideCursor = null;
    }

    /// <summary>
    /// Captures a single frame from the camera and initiates the recording process.
    /// </summary>
    /// <remarks>
    /// This method interacts with the underlying <see cref="MedWandController"/> to start recording
    /// a frame from the camera. It is typically invoked as part of user actions or automated workflows
    /// requiring a snapshot or frame capture.
    /// </remarks>
    internal void CaptureFrame()
    {
        _medWandController.StartRecording();
    }

    /// <summary>
    /// Releases all resources used by the <see cref="CameraViewModel"/> instance.
    /// </summary>
    /// <remarks>
    /// This method unsubscribes from events and stops any active timers associated with the camera.
    /// It ensures proper cleanup of resources to prevent memory leaks or unexpected behavior.
    /// </remarks>
    public void Dispose()
    {
        if (_medWandController.CameraHasOnTimer)
        {
            if (_controlTimer != null)
            {
                _controlTimer.Tick -= OnControlTimerTick;
                _controlTimer.Stop();
            }
        }
        if (_medWandController.Camera == null) return;
        _medWandController.OnLedIntensityChanged -= MedWandController_LedIntensityChanged;
        _medWandController.Camera.RecordedFrameReady -= Camera_RecordedFrameReady;

    }

    /// <summary>
    /// Sets the interactive state of various camera control elements.
    /// </summary>
    /// <param name="enabled">
    /// A boolean value indicating whether the controls should be enabled (<c>true</c>) or disabled (<c>false</c>).
    /// </param>
    /// <remarks>
    /// This method updates the enabled state of buttons and sliders related to camera functionalities,
    /// such as LED intensity, focus adjustment, and specific camera mode buttons.
    /// </remarks>
    private void SetControlsInteractive(bool enabled)
    {
        ButtonOffEnabled = enabled;
        ButtonDermatoscopeEnabled = enabled;
        ButtonOtoscopeEnabled = enabled;
        LedIntensitySliderEnabled = _medWandController.CameraLedIntensityAdjustable && enabled;
        FocusIntensitySliderEnabled = enabled;
        ButtonActionEnabled = enabled;
    }

    /// <summary>
    /// Updates the current status of the camera, including the reading state, 
    /// status message, and other related properties based on the camera's mode 
    /// and operational state.
    /// </summary>
    /// <remarks>
    /// This method determines the reading state of the camera and constructs 
    /// a status message that reflects the current camera mode, reading state, 
    /// and additional details such as captured frames or timer availability.
    /// </remarks>
    private void UpdateStatus()
    {
        var focusState = "";
        if (_medWandController.CameraFocusMode == FocusModes.Manual)
        {
            focusState = $"({_focusValue})";
        }
        _readingState = _medWandController.ReadingState switch
        {
            ReadingState.Stopped => "Ready",
            ReadingState.Starting => "On",
            ReadingState.Started => "On",
            ReadingState.Reading => "On",
            _ => _medWandController.ReadingState.ToString()
        };
        if (!_medWandController.CameraHasOnTimer)
        {
            StatusMessage = $"{_medWandController.CameraMode} : {_readingState} Focus: {_medWandController.CameraFocusMode}{focusState} [{_framesCaptured} Captured]";
        }
        else
        {
            if (_controlTimerMinCoolDown) _readingState = "Cooldown";
            StatusMessage = $"{_medWandController.CameraMode} : {_readingState} ({_controlTimerTickSeconds}s available) [{_framesCaptured} Captured]";
        }
    }

    /// <summary>
    /// Updates the current action state of the camera view and adjusts the UI and behavior accordingly.
    /// </summary>
    /// <param name="actionState">The new action state to set. Possible values are <see cref="ActionState.Idle"/>, <see cref="ActionState.Busy"/>, and <see cref="ActionState.Disabled"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="actionState"/> is not a valid <see cref="ActionState"/> value.
    /// </exception>
    /// <remarks>
    /// This method modifies the state of the action button and triggers the <see cref="ViewLockStateChanged"/> event
    /// to notify subscribers about changes in the view's lock state.
    /// </remarks>
    private void SetAction(ActionState actionState)
    {
        switch (actionState)
        {
            case ActionState.Idle:
                SetActionButtonIdle();
                ViewLockStateChanged?.Invoke(false);
                break;
            case ActionState.Busy:
                ViewLockStateChanged?.Invoke(true);
                SetActionButtonBusy();
                break;
            case ActionState.Disabled:
                SetActionButtonDisabled();
                ViewLockStateChanged?.Invoke(false);
                break;
            default:
                ViewLockStateChanged?.Invoke(false);
                throw new ArgumentOutOfRangeException(nameof(actionState), actionState, null);
        }
    }

    /// <summary>
    /// Updates the state of the action button to indicate that an action is in progress.
    /// </summary>
    /// <remarks>
    /// This method sets the action button's state to <see cref="ActionState.Busy"/>, updates its text to
    /// display a "Capturing..." message, and assigns a tag representing the busy state.
    /// </remarks>
    private void SetActionButtonBusy()
    {
        ButtonActionState = ActionState.Busy;
        ButtonActionText = "Capturing...";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    /// <summary>
    /// Configures the action button to represent the "Idle" state.
    /// </summary>
    /// <remarks>
    /// This method updates the action button's state, text, and tag to indicate that it is in the "Idle" state.
    /// It is typically used when the application is ready for user interaction, such as capturing a frame.
    /// </remarks>
    private void SetActionButtonIdle()
    {
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Capture";
        ButtonActionTag = nameof(ActionState.Idle);
    }

    /// <summary>
    /// Updates the state of the action button to indicate it is disabled.
    /// </summary>
    /// <remarks>
    /// This method sets the <see cref="ButtonActionState"/> to <see cref="ActionState.Disabled"/>, 
    /// clears the <see cref="ButtonActionText"/>, and assigns the <see cref="ButtonActionTag"/> 
    /// to represent the disabled state.
    /// </remarks>
    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = "";
        ButtonActionTag = nameof(ActionState.Disabled);
    }


    #region Events

    /// <summary>
    /// Handles the tick event of the control timer.
    /// </summary>
    /// <param name="sender">The source of the event, typically the timer instance.</param>
    /// <param name="e">The event data associated with the timer tick.</param>
    /// <remarks>
    /// This method updates the timer counters and manages the camera's operational state,
    /// including transitioning between active and cooldown states. It also updates the UI
    /// and control interactivity based on the timer's progress.
    /// </remarks>
    private void OnControlTimerTick(object? sender, EventArgs e)
    {
        if (_controlTimerTickDown)
        {
            _controlTimerTickCounter--;
            _controlTimerTickSeconds--;
            if (_controlTimerTickSeconds <= 0)
            {
                _controlTimerMinCoolDown = true;
                SetCameraMode(CameraModes.Off);
                SetControlsInteractive(false);
            }
        }
        else
        {
            _controlTimerTickCounter++;
            if (_controlTimerTickCounter % 3 == 0)
            {
                _controlTimerTickSeconds++;
                if (_controlTimerMinCoolDown && _controlTimerTickSeconds >= _medWandController.CameraCoolTimeMin)
                {
                    _controlTimerMinCoolDown = false;
                    SetControlsInteractive(true);
                }
                else if (_controlTimerTickSeconds >= _medWandController.CameraOnTimeMax)
                {
                    _controlTimer?.Stop();
                    _controlTimerTickCounter = _medWandController.CameraOnTimeMax;
                    _controlTimerTickSeconds = _medWandController.CameraOnTimeMax;
                }
            }
        }
        UpdateStatus();
    }

    /// <summary>
    /// Handles the LED intensity change event from the MedWand controller.
    /// </summary>
    /// <param name="value">The new LED intensity value provided by the MedWand controller.</param>
    /// <remarks>
    /// This method updates the <see cref="LedIntensity"/> property to reflect the new LED intensity value.
    /// It is triggered when the MedWand controller raises the <c>OnLedIntensityChanged</c> event.
    /// </remarks>
    private void MedWandController_LedIntensityChanged(int value)
    {
        LedIntensity = value;
    }

    /// <summary>
    /// Handles the event triggered when a recorded frame is ready from the camera.
    /// </summary>
    /// <param name="sender">The source of the event, typically the camera object.</param>
    /// <param name="bytes">The byte array containing the recorded frame data.</param>
    /// <remarks>
    /// This method appends information about the captured frame to a log file, updates the frame count,
    /// refreshes the status, and resets the mouse cursor.
    /// </remarks>
    private void Camera_RecordedFrameReady(object? sender, byte[] bytes)
    {
        File.AppendAllText("captures.txt", $"[{DateTime.UtcNow:O}] {_medWandController.CameraModel} {CameraMode} -> {_medWandController.CameraBmpFromCapture(bytes)}\n");
        _framesCaptured++;
        UpdateStatus();
        Mouse.OverrideCursor = null;
    }

    #endregion



    #region Bindings

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _statusMessage = "Starting";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _videoPreview.SafeInvoke(() =>
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged();
            });
        }
    }

    private ActionState _buttonActionState = ActionState.Idle;
    public ActionState ButtonActionState
    {
        get => _buttonActionState;
        set
        {
            if (_buttonActionState == value) return;
            _buttonActionState = value;
            OnPropertyChanged();
        }
    }

    private string _buttonActionTag = nameof(ActionState.Idle);
    public string ButtonActionTag
    {
        get => _buttonActionTag;
        set
        {
            if (_buttonActionTag == value) return;
            _buttonActionTag = value;
            OnPropertyChanged();
        }
    }

    private string _buttonActionText = "Capture";
    public string ButtonActionText
    {
        get => _buttonActionText;
        set
        {
            if (_buttonActionText == value) return;
            _buttonActionText = value;
            OnPropertyChanged();
        }
    }

    private bool _buttonActionEnabled = true;
    public bool ButtonActionEnabled
    {
        get => _buttonActionEnabled;
        set
        {
            if (_buttonActionEnabled == value) return;
            _buttonActionEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _buttonOffEnabled = true;
    public bool ButtonOffEnabled
    {
        get => _buttonOffEnabled;
        set
        {
            if (_buttonOffEnabled == value) return;
            _buttonOffEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _buttonDermatoscopeEnabled = true;
    public bool ButtonDermatoscopeEnabled
    {
        get => _buttonDermatoscopeEnabled;
        set
        {
            if (_buttonDermatoscopeEnabled == value) return;
            _buttonDermatoscopeEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _buttonOtoscopeEnabled = true;
    public bool ButtonOtoscopeEnabled
    {
        get => _buttonOtoscopeEnabled;
        set
        {
            if (_buttonOtoscopeEnabled == value) return;
            _buttonOtoscopeEnabled = value;
            OnPropertyChanged();
        }
    }

    private Visibility _videoOverlayVisible = Visibility.Visible;
    public Visibility VideoOverlayVisible
    {
        get => _videoOverlayVisible;
        set
        {
            if (_videoOverlayVisible == value) return;
            _videoOverlayVisible = value;
            OnPropertyChanged();
        }
    }
    
    public string LedIntensityString => $"LED Intensity ({_ledIntensity})";
    private Visibility _ledIntensityStringVisible = Visibility.Collapsed;
    public Visibility LedIntensityStringVisible
    {
        get => _ledIntensityStringVisible;
        set
        {
            if (_ledIntensityStringVisible == value) return;
            _ledIntensityStringVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _ledIntensityStringEnabled = true;
    public bool LedIntensityStringEnabled
    {
        get => _ledIntensityStringEnabled;
        set
        {
            if (_ledIntensityStringEnabled == value) return;
            _ledIntensityStringEnabled = value;
            OnPropertyChanged();
        }
    }

    private int _ledIntensityMax;
    public int LedIntensityMax
    {
        get => _ledIntensityMax;
        set
        {
            if (_ledIntensityMax == value) return;
            _ledIntensityMax = value;
            OnPropertyChanged();
        }
    }
    
    private int _ledIntensity;
    public int LedIntensity
    {
        get => _ledIntensity;
        set
        {
            if (_ledIntensity == value) return;
            _ledIntensity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LedIntensityString));
        }
    }

    private Visibility _ledIntensitySliderVisible = Visibility.Collapsed;
    public Visibility LedIntensitySliderVisible
    {
        get => _ledIntensitySliderVisible;
        set
        {
            if (_ledIntensitySliderVisible == value) return;
            _ledIntensitySliderVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _ledIntensitySliderEnabled = true;
    public bool LedIntensitySliderEnabled
    {
        get => _ledIntensitySliderEnabled;
        set
        {
            if (_ledIntensitySliderEnabled == value) return;
            _ledIntensitySliderEnabled = value;
            OnPropertyChanged();
        }
    }


    private string _tipMaskInfo = "";
    public string TipMaskInfo
    {
        get => _tipMaskInfo;
        set
        {
            if (_tipMaskInfo == value) return;
            _tipMaskInfo = value;
            OnPropertyChanged();
        }
    }

    private Visibility _tipMaskInfoVisible = Visibility.Visible;
    public Visibility TipMaskInfoVisible
    {
        get => _tipMaskInfoVisible;
        set
        {
            if (_tipMaskInfoVisible == value) return;
            _tipMaskInfoVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _tipMaskInfoEnabled = true;
    public bool TipMaskInfoEnabled
    {
        get => _tipMaskInfoEnabled;
        set
        {
            if (_tipMaskInfoEnabled == value) return;
            _tipMaskInfoEnabled = value;
            OnPropertyChanged();
        }
    }

    private int _focusIntensityMax;
    public int FocusIntensityMax
    {
        get => _focusIntensityMax;
        set
        {
            if (_focusIntensityMax == value) return;
            _focusIntensityMax = value;
            OnPropertyChanged();
        }
    }

    private int _focusIntensity;
    public int FocusIntensity
    {
        get => _focusIntensity;
        set
        {
            if (_focusIntensity == value) return;
            _focusIntensity = value;
            OnPropertyChanged();
        }
    }

    private Visibility _focusIntensitySliderVisible = Visibility.Collapsed;
    public Visibility FocusIntensitySliderVisible
    {
        get => _focusIntensitySliderVisible;
        set
        {
            if (_focusIntensitySliderVisible == value) return;
            _focusIntensitySliderVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _focusIntensitySliderEnabled = true;
    public bool FocusIntensitySliderEnabled
    {
        get => _focusIntensitySliderEnabled;
        set
        {
            if (_focusIntensitySliderEnabled == value) return;
            _focusIntensitySliderEnabled = value;
            OnPropertyChanged();
        }
    }

    #endregion

}