using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Core;
using static MWSDK.NetCore.Internal.CameraHelper;

namespace SampleWpfApp.Views;

public sealed class CameraViewModel : INotifyPropertyChanged, IDisposable
{
    public MedWandSensor MedWandSensor => MedWandSensor.Otoscope;
    public CameraModes CameraMode => _medWandController.CameraMode;
    public event Action<bool>? ViewLockStateChanged;
    public event Action<CameraModes>? CameraModeChanged;

    private readonly MedWandController _medWandController;
    private DispatcherTimer? _controlTimer;
    private readonly Image _videoPreview;
    private bool _isActivated;
    private int _framesCaptured;
    private bool _controlTimerTickDown = true;
    private bool _controlTimerMinCoolDown;
    private int _controlTimerTickCounter;
    private int _controlTimerTickSeconds;
    private string _readingState = string.Empty;

    public CameraViewModel(MedWandController medWandController, Image videoPreview)
    {
        _medWandController = medWandController;
        _videoPreview = videoPreview;
        LedIntensityMax = _medWandController.CameraLedIntensityMax;
    }

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
                _medWandController.LedIntensityChanged += MedWandController_LedIntensityChanged;
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

    internal void Deactivate()
    {
        SetCameraMode(CameraModes.Off);
    }

    internal bool KeyDown(Key key)
    {
        switch (key)
        {
            case Key.Left:
                _medWandController.CameraMove(-1, null);
                return true;
            case Key.Right:
                _medWandController.CameraMove(1, null);
                return true;
            case Key.Up:
                _medWandController.CameraMove(null, -1);
                return true;
            case Key.Down:
                _medWandController.CameraMove(null, 1);
                return true;
            case Key.PageUp:
                _medWandController.CameraZoom(1);
                return true;
            case Key.PageDown:
                _medWandController.CameraZoom(-1);
                return true;
            case Key.Enter:
                _medWandController.CameraReset();
                return true;
        }

        return false;
    }

    internal void SetCameraMode(CameraModes cameraMode)
    {
        if (cameraMode == _medWandController.CameraMode)
        {
            return;
        }

        Mouse.OverrideCursor = Cursors.Wait;

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

        Mouse.OverrideCursor = null;
    }

    internal void CaptureFrame()
    {
        _medWandController.StartRecording();
    }

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
        _medWandController.LedIntensityChanged -= MedWandController_LedIntensityChanged;
        _medWandController.Camera.RecordedFrameReady -= Camera_RecordedFrameReady;

    }

    private void SetControlsInteractive(bool enabled)
    {
        ButtonOffEnabled = enabled;
        ButtonDermatoscopeEnabled = enabled;
        ButtonOtoscopeEnabled = enabled;
        LedIntensitySliderEnabled = _medWandController.CameraLedIntensityAdjustable && enabled;
        FocusIntensitySliderEnabled = enabled;
        ButtonActionEnabled = enabled;
    }

    private void UpdateStatus()
    {
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
            StatusMessage = $"{_medWandController.CameraMode} : {_readingState} [{_framesCaptured} Captured]";
        }
        else
        {
            if (_controlTimerMinCoolDown) _readingState = "Cooldown";
            StatusMessage = $"{_medWandController.CameraMode} : {_readingState} ({_controlTimerTickSeconds}s available) [{_framesCaptured} Captured]";
        }
    }

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

    private void SetActionButtonBusy()
    {
        ButtonActionState = ActionState.Busy;
        ButtonActionText = "Capturing...";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    private void SetActionButtonIdle()
    {
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Capture";
        ButtonActionTag = nameof(ActionState.Idle);
    }

    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = "";
        ButtonActionTag = nameof(ActionState.Disabled);
    }


    #region Events

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

    private void MedWandController_LedIntensityChanged(int value)
    {
        LedIntensity = value;
    }

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
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
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