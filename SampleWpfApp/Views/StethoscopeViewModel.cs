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

/// <summary>
/// Represents the ViewModel for the Stethoscope functionality in the application.
/// </summary>
/// <remarks>
/// This class provides properties, commands, and event handlers to manage the state and behavior
/// of the stethoscope sensor, including mode selection, recording control, and UI updates.
/// It implements <see cref="System.ComponentModel.INotifyPropertyChanged"/> to notify the UI of property changes
/// and <see cref="System.IDisposable"/> to release resources when no longer needed.
/// </remarks>
public sealed class StethoscopeViewModel : INotifyPropertyChanged, IDisposable
{
    public MedWandSensor MedWandSensor => MedWandSensor.Stethoscope;
    public MicrophoneModes StethoscopeMode => _medWandController.StethoscopeMode;
    public event Action<bool>? ViewLockStateChanged;
    public event Action<MicrophoneModes>? StethoscopeModeChanged;

    private readonly MedWandController _medWandController;
    private readonly Action<bool> _setLocked;
    private bool _isActivated;
    private int _framesCaptured;
    private string _readingState = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="StethoscopeViewModel"/> class.
    /// </summary>
    /// <param name="medWandController">
    /// The <see cref="MedWandController"/> instance used to manage the stethoscope sensor and its operations.
    /// </param>
    /// <param name="setLocked">
    /// An <see cref="Action{T}"/> delegate to handle the locked state of the view.
    /// </param>
    /// <remarks>
    /// This constructor sets up the necessary dependencies for the stethoscope functionality,
    /// including the controller that handles sensor interactions.
    /// </remarks>
    public StethoscopeViewModel(MedWandController medWandController, Action<bool> setLocked)
    {
        _medWandController = medWandController;
        _setLocked = setLocked;
    }

    /// <summary>
    /// Activates the stethoscope functionality by initializing necessary event handlers 
    /// and updating the initial state of the ViewModel.
    /// </summary>
    /// <remarks>
    /// This method ensures that the stethoscope is properly set up for use by attaching 
    /// event handlers and setting the initial action state and status. It prevents 
    /// multiple activations by checking the activation state.
    /// </remarks>
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

    /// <summary>
    /// Deactivates the stethoscope functionality by resetting its mode and detaching event handlers.
    /// </summary>
    /// <remarks>
    /// This method sets the stethoscope mode to <see cref="StethoscopeHelpers.MicrophoneModes.Off"/> 
    /// and removes the event handler for recorded frames to release resources and stop ongoing operations.
    /// </remarks>
    internal void Deactivate()
    {
        SetStethoscopeMode(MicrophoneModes.Off);
        if (_medWandController.Stethoscope == null) return;
        _medWandController.Stethoscope.RecordedFramesReady -= OnRecordedFramesReady;
    }

    /// <summary>
    /// Sets the mode of the stethoscope to the specified value.
    /// </summary>
    /// <param name="stethoscopeMode">The desired mode for the stethoscope, represented by <see cref="StethoscopeHelpers.MicrophoneModes"/>.</param>
    /// <remarks>
    /// This method updates the stethoscope mode if it differs from the current mode. It temporarily sets the mouse cursor 
    /// to a wait cursor during the operation, invokes the mode change on the underlying controller, and triggers the 
    /// <see cref="StethoscopeModeChanged"/> event to notify subscribers of the mode change. Additionally, it updates the 
    /// status of the ViewModel to reflect the new mode.
    /// </remarks>
    internal void SetStethoscopeMode(MicrophoneModes stethoscopeMode)
    {
        if (stethoscopeMode == _medWandController.StethoscopeMode)
        {
            return;
        }

        Mouse.OverrideCursor = Cursors.Wait;
        _setLocked(true);

        _medWandController.SetStethoscopeMode(stethoscopeMode, null);

        StethoscopeModeChanged?.Invoke(_medWandController.StethoscopeMode);

        UpdateStatus();

        _setLocked(true);
        Mouse.OverrideCursor = null;
    }

    /// <summary>
    /// Initiates the process of capturing audio data using the stethoscope sensor.
    /// </summary>
    /// <remarks>
    /// This method sets the action state to <see cref="ActionState.Disabled"/>, starts the recording process
    /// via the associated <see cref="MedWandController"/>, and then updates the action state to <see cref="ActionState.Busy"/>.
    /// </remarks>
    internal void StartCapture()
    {
        SetAction(ActionState.Disabled);
        _medWandController.StartRecording();
        SetAction(ActionState.Busy);
    }

    /// <summary>
    /// Stops the ongoing audio capture process for the stethoscope.
    /// </summary>
    /// <remarks>
    /// This method transitions the stethoscope's action state to <see cref="ActionState.Disabled"/>,
    /// stops the recording process through the associated <see cref="MedWandController"/>,
    /// and then sets the action state to <see cref="ActionState.Idle"/>.
    /// </remarks>
    internal void StopCapture()
    {
        SetAction(ActionState.Disabled);
        _medWandController.StopRecording();
        SetAction(ActionState.Idle);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="StethoscopeViewModel"/> instance.
    /// </summary>
    /// <remarks>
    /// This method unsubscribes from events and performs cleanup to ensure that resources
    /// are properly released. It should be called when the ViewModel is no longer needed
    /// to avoid memory leaks.
    /// </remarks>
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

    /// <summary>
    /// Updates the current status of the stethoscope, including its mode, reading state, 
    /// and the number of frames captured. This method constructs a status message 
    /// reflecting the stethoscope's operational state.
    /// </summary>
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

    /// <summary>
    /// Updates the current action state and adjusts the UI and view lock state accordingly.
    /// </summary>
    /// <param name="actionState">The new action state to set. Possible values are <see cref="ActionState.Idle"/>, <see cref="ActionState.Busy"/>, or <see cref="ActionState.Disabled"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid <paramref name="actionState"/> is provided.</exception>
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
    /// Updates the state of the action button to indicate that it is busy.
    /// </summary>
    /// <remarks>
    /// This method sets the action button's state to <see cref="ActionState.Busy"/>, updates its text to "Stop Recording",
    /// and assigns the corresponding tag to reflect the busy state.
    /// It is typically invoked when the application is performing an ongoing operation that requires user attention.
    /// </remarks>
    private void SetActionButtonBusy()
    {
        ButtonActionState = ActionState.Busy;
        ButtonActionText = "Stop Recording";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    /// <summary>
    /// Configures the action button to represent the idle state.
    /// </summary>
    /// <remarks>
    /// This method updates the action button's state, text, and tag to reflect that
    /// the stethoscope is ready for a new action, such as starting a recording.
    /// </remarks>
    private void SetActionButtonIdle()
    {
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start Recording";
        ButtonActionTag = nameof(ActionState.Idle);
    }

    /// <summary>
    /// Updates the state of the action button to indicate it is disabled.
    /// </summary>
    /// <remarks>
    /// This method sets the <see cref="ButtonActionState"/> to <see cref="ActionState.Disabled"/>, 
    /// clears the <see cref="ButtonActionText"/>, and assigns the <see cref="ButtonActionTag"/> 
    /// to the name of the <see cref="ActionState.Disabled"/> state.
    /// </remarks>
    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = "";
        ButtonActionTag = nameof(ActionState.Disabled);
    }


    #region Events

    /// <summary>
    /// Handles the event triggered when recorded frames are ready from the stethoscope.
    /// </summary>
    /// <param name="sender">The source of the event, typically the stethoscope device.</param>
    /// <param name="bytes">The byte array containing the recorded audio frames.</param>
    /// <remarks>
    /// This method processes the recorded frames by appending relevant information to a log file,
    /// updating the frame capture count, and refreshing the UI status.
    /// </remarks>
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

    private int _gain;
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