using MWSDK.NetCore;
using MWSDK.NetCore.Internal;
using MWSDK.Wpf;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SampleWpfApp.Core;
using static MWSDK.NetCore.Internal.StethoscopeHelpers;

namespace SampleWpfApp.Views;

public sealed class StethoscopeViewModel : INotifyPropertyChanged, IDisposable
{
    public MedWandSensor MedWandSensor => MedWandSensor.Stethoscope;
    public StethoscopeHelpers.MicrophoneModes StethoscopeMode => _medWandController.StethoscopeMode;
    public event Action<bool>? ViewLockStateChanged;
    public event Action<StethoscopeHelpers.MicrophoneModes>? StethoscopeModeChanged;

    private readonly MedWandController _medWandController;
    private bool _isActivated;
    private int _framesCaptured;
    private string _readingState = string.Empty;

    public StethoscopeViewModel(MedWandController medWandController)
    {
        _medWandController = medWandController;
    }

    internal void Activate()
    {
        if (!_isActivated)
        {
            _isActivated = true;
            if (_medWandController.Stethoscope != null)
            {
                // Add event handlers
                _medWandController.Stethoscope.RecordedFramesReady += OnRecordedFramesReady;
            }
        }
        SetAction(ActionState.Idle);
        UpdateStatus();
    }

    internal void Deactivate()
    {
        SetStethoscopeMode(MicrophoneModes.Off);
        if (_medWandController.Stethoscope == null) return;
        _medWandController.Stethoscope.RecordedFramesReady -= OnRecordedFramesReady;
    }

    internal void SetStethoscopeMode(MicrophoneModes stethoscopeMode)
    {
        if (stethoscopeMode == _medWandController.StethoscopeMode)
        {
            return;
        }

        Mouse.OverrideCursor = Cursors.Wait;

        _medWandController.SetStethoscopeMode(stethoscopeMode, null);

        StethoscopeModeChanged?.Invoke(_medWandController.StethoscopeMode);

        UpdateStatus();

        Mouse.OverrideCursor = null;
    }

    internal void StartCapture()
    {
        SetAction(ActionState.Disabled);
        _medWandController.StartRecording();
        SetAction(ActionState.Busy);
    }

    internal void StopCapture()
    {
        SetAction(ActionState.Disabled);
        _medWandController.StopRecording();
        SetAction(ActionState.Idle);
    }

    public void Dispose()
    {
        if (_medWandController.Stethoscope == null) return;
        _medWandController.Stethoscope.RecordedFramesReady -= OnRecordedFramesReady;
    }

    private void SetControlsInteractive(bool enabled)
    {
        ButtonOffEnabled = enabled;
        ButtonHeartEnabled = enabled;
        ButtonLungsEnabled = enabled;
        ButtonBowelEnabled = enabled;
        VolumeSliderEnabled = enabled;
        GainSliderEnabled = enabled;
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
        StatusMessage = $"{_medWandController.StethoscopeMode} : {_readingState} [{_framesCaptured} Captured]";
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
        ButtonActionText = "Stop Recording";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    private void SetActionButtonIdle()
    {
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start Recording";
        ButtonActionTag = nameof(ActionState.Idle);
    }

    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = "";
        ButtonActionTag = nameof(ActionState.Disabled);
    }


    #region Events

    private void OnRecordedFramesReady(object? sender, byte[] bytes)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        File.AppendAllText("captures.txt", $"[{DateTime.UtcNow:O}] {_medWandController.StethoscopeModel} {StethoscopeMode} -> {_medWandController.StethoscopeWavFromCapture(bytes)}\n");
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

    private string _buttonActionText = "Start Recording";
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

    private bool _buttonHeartEnabled = true;
    public bool ButtonHeartEnabled
    {
        get => _buttonHeartEnabled;
        set
        {
            if (_buttonHeartEnabled == value) return;
            _buttonHeartEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _buttonLungsEnabled = true;
    public bool ButtonLungsEnabled
    {
        get => _buttonLungsEnabled;
        set
        {
            if (_buttonLungsEnabled == value) return;
            _buttonLungsEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _buttonBowelEnabled = true;
    public bool ButtonBowelEnabled
    {
        get => _buttonBowelEnabled;
        set
        {
            if (_buttonBowelEnabled == value) return;
            _buttonBowelEnabled = value;
            OnPropertyChanged();
        }
    }


    private string _volumeString = "Volume";
    public string VolumeString
    {
        get => _volumeString;
        set
        {
            if (_volumeString == value) return;
            _volumeString = value;
            OnPropertyChanged();
        }
    }

    private bool _volumeStringVisible = true;
    public bool VolumeStringVisible
    {
        get => _volumeStringVisible;
        set
        {
            if (_volumeStringVisible == value) return;
            _volumeStringVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _volumeStringEnabled = true;
    public bool VolumeStringEnabled
    {
        get => _volumeStringEnabled;
        set
        {
            if (_volumeStringEnabled == value) return;
            _volumeStringEnabled = value;
            OnPropertyChanged();
        }
    }

    private int _volumeMax = 100;
    public int VolumeMax
    {
        get => _volumeMax;
        set
        {
            if (_volumeMax == value) return;
            _volumeMax = value;
            OnPropertyChanged();
        }
    }

    private int _volume = 15;
    public int Volume
    {
        get => _volume;
        set
        {
            if (_volume == value) return;
            _volume = value;
            OnPropertyChanged();
        }
    }

    private bool _volumeSliderVisible = true;
    public bool VolumeSliderVisible
    {
        get => _volumeSliderVisible;
        set
        {
            if (_volumeSliderVisible == value) return;
            _volumeSliderVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _volumeSliderEnabled = true;
    public bool VolumeSliderEnabled
    {
        get => _volumeSliderEnabled;
        set
        {
            if (_volumeSliderEnabled == value) return;
            _volumeSliderEnabled = value;
            OnPropertyChanged();
        }
    }


    private string _gainString = "Gain";
    public string GainString
    {
        get => _gainString;
        set
        {
            if (_gainString == value) return;
            _gainString = value;
            OnPropertyChanged();
        }
    }

    private bool _gainStringVisible = true;
    public bool GainStringVisible
    {
        get => _gainStringVisible;
        set
        {
            if (_gainStringVisible == value) return;
            _gainStringVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _gainStringEnabled = true;
    public bool GainStringEnabled
    {
        get => _gainStringEnabled;
        set
        {
            if (_gainStringEnabled == value) return;
            _gainStringEnabled = value;
            OnPropertyChanged();
        }
    }

    private int _gainMax = 10;
    public int GainMax
    {
        get => _gainMax;
        set
        {
            if (_gainMax == value) return;
            _gainMax = value;
            OnPropertyChanged();
        }
    }

    private int _gain = 0;
    public int Gain
    {
        get => _gain;
        set
        {
            if (_gain == value) return;
            _gain = value;
            OnPropertyChanged();
        }
    }

    private bool _gainSliderVisible = true;
    public bool GainSliderVisible
    {
        get => _gainSliderVisible;
        set
        {
            if (_gainSliderVisible == value) return;
            _gainSliderVisible = value;
            OnPropertyChanged();
        }
    }

    private bool _gainSliderEnabled = true;
    public bool GainSliderEnabled
    {
        get => _gainSliderEnabled;
        set
        {
            if (_gainSliderEnabled == value) return;
            _gainSliderEnabled = value;
            OnPropertyChanged();
        }
    }

    #endregion

}