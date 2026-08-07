using MWSDK.NetCore;
using MWSDK.Wpf;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SampleWpfApp.Core;

namespace SampleWpfApp.Views;

/// <summary>
/// Represents the ViewModel for managing ECG (Electrocardiogram) operations in the application.
/// </summary>
/// <remarks>
/// This class is responsible for handling the state and behavior of ECG-related functionality, 
/// including starting and stopping ECG monitoring, managing the recording process, and handling 
/// device errors. It interacts with the <see cref="MedWandController"/> to control the ECG sensor 
/// and provides properties for binding to the UI.
/// </remarks>
public sealed class EcgViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MedWandController _medWandController;
    private readonly Action<bool> _setLocked;
    private MedWandReading? _reading;
    private bool _isActivated;
    private int _captured;

    /// <summary>
    /// Initializes a new instance of the <see cref="EcgViewModel"/> class.
    /// </summary>
    /// <param name="medWandController">
    /// The <see cref="MedWandController"/> instance used to interact with the ECG sensor.
    /// </param>
    /// <param name="setLocked">
    /// An <see cref="Action{T}"/> delegate to manage the lock state of the view.
    /// </param>
    /// <remarks>
    /// This constructor sets up the initial state of the ViewModel, including initializing 
    /// the status message, button action state, and other properties required for ECG monitoring.
    /// </remarks>
    public EcgViewModel(
        MedWandController medWandController,
        Action<bool> setLocked)
    {
        _medWandController = medWandController;
        _setLocked = setLocked;

        StatusMessage = "Starting";
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start Recording";
        ButtonActionTag = nameof(ActionState.Idle);
    }


    /// <summary>
    /// Activates the ECG monitoring functionality by initializing the necessary components 
    /// and setting up event handlers for the ECG sensor.
    /// </summary>
    /// <remarks>
    /// This method ensures that the ECG monitoring is prepared for operation by:
    /// - Setting the activation state.
    /// - Initializing a new <see cref="MedWandReading"/> instance.
    /// - Attaching event handlers to the ECG sensor for handling recorded strip data.
    /// - Updating the action state and status message.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the ECG sensor is not available in the <see cref="MedWandController"/>.
    /// </exception>
    internal void Activate()
    {
        if (!_isActivated)
        {
            _isActivated = true;
            if (_medWandController.Ecg != null)
            {
                // Add event handlers
                _medWandController.Ecg.RecordedStripReady += Ecg_RecordedStripReady;
            }
        }
        _reading = new MedWandReading
        {
            TimeStamp = DateTime.UtcNow,
            Status = string.Empty,
            Index = 1,
            Count = 0,
            SensorType = nameof(MedWandSensor.Ecg),
            TempAmbient = string.Empty,
            TempObject = string.Empty,
            PulseRate = null,
            Spo2 = null,
            EcgData = null
        };
        SetAction(ActionState.Idle);
        SetStatus("Monitoring");
    }

    /// <summary>
    /// Deactivates the ECG monitoring process and cleans up related resources.
    /// </summary>
    /// <remarks>
    /// This method stops the ECG sensor, unsubscribes from the <see cref="MedWandController.Ecg.RecordedStripReady"/> event, 
    /// and updates the status to indicate that monitoring has stopped.
    /// </remarks>
    internal void Deactivate()
    {
        StopSensor();
        if (_medWandController.Ecg == null) return;
        _medWandController.Ecg.RecordedStripReady -= Ecg_RecordedStripReady;
        SetStatus("Not Monitoring");
    }

    /// <summary>
    /// Handles changes in the reading state of the ECG sensor.
    /// </summary>
    /// <param name="state">The new <see cref="ReadingState"/> of the ECG sensor.</param>
    /// <remarks>
    /// This method updates the status message to reflect the current reading state.
    /// It is typically invoked when the sensor's state changes, such as starting, stopping, or encountering an error.
    /// </remarks>
    public void OnReadingStateChanged(ReadingState state)
    {
        SetStatus(state.ToString());
    }

    /// <summary>
    /// Handles the event when a new ECG reading is received.
    /// </summary>
    /// <param name="reading">
    /// The <see cref="MedWandReading"/> instance containing the data of the received ECG reading.
    /// </param>
    /// <remarks>
    /// This method updates the current reading with the provided data. It is typically invoked 
    /// when the ECG sensor provides a new reading.
    /// </remarks>
    public void OnReadingReceived(MedWandReading reading)
    {
        _reading = reading;
    }

    /// <summary>
    /// Handles device errors encountered during ECG operations.
    /// </summary>
    /// <param name="error">
    /// The <see cref="MedWandDeviceError"/> instance representing the error encountered, 
    /// or <c>null</c> if there is no specific error.
    /// </param>
    /// <remarks>
    /// This method updates the action state of the ViewModel based on the presence or absence of a device error. 
    /// If an error is provided, the action state is set to <see cref="ActionState.Disabled"/>; otherwise, it is set to <see cref="ActionState.Idle"/>.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown if an unexpected error occurs while handling the device error.
    /// </exception>
    public void OnDeviceError(MedWandDeviceError? error)
    {
        try
        {
            SetAction(error == null ? ActionState.Idle : ActionState.Disabled);
        }
        catch (Exception outerEx)
        {
            Debug.WriteLine(outerEx.Message);
        }
    }

    /// <summary>
    /// Handles the click event of the action button in the ECG View.
    /// </summary>
    /// <remarks>
    /// This method changes the state of the ECG monitoring process based on the current 
    /// <see cref="ButtonActionState"/>. It initiates or stops the ECG recording by calling 
    /// <see cref="StartCapture"/> or <see cref="StopCapture"/> respectively. If the button 
    /// is in the <see cref="ActionState.Disabled"/> state, no action is performed.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <see cref="ButtonActionState"/> is set to an unexpected value.
    /// </exception>
    public void OnActionButtonClick()
    {
        switch (ButtonActionState)
        {
            case ActionState.Idle:
                StartCapture();
                break;
            case ActionState.Busy:
                StopCapture();
                break;
            case ActionState.Disabled:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Starts the ECG sensor for monitoring and recording data.
    /// </summary>
    /// <remarks>
    /// This method interacts with the <see cref="MedWandController"/> to initiate the ECG sensor. 
    /// It sets the action state to <see cref="ActionState.Disabled"/> during the initialization process 
    /// and locks the view to prevent user interaction. If the sensor starts successfully, the action 
    /// state is updated to <see cref="ActionState.Idle"/>. Otherwise, the action state is reverted, 
    /// and the view is unlocked.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown if an error occurs while attempting to start the ECG sensor. The exception is logged, 
    /// and the action state is updated accordingly.
    /// </exception>
    internal void StartSensor()
    {
        try
        {
            SetAction(ActionState.Disabled);
            _setLocked(true);

            if (_medWandController.StartEcg())
            {
                SetAction(ActionState.Idle);
            }
            else
            {
                SetAction(ActionState.Disabled);
                _setLocked(false);
            }
        }
        catch (Exception outerEx)
        {
            Debug.WriteLine(outerEx.Message);
            SetAction(ActionState.Disabled);
            _setLocked(false);
        }
    }

    /// <summary>
    /// Stops the ECG sensor and resets the action state.
    /// </summary>
    /// <remarks>
    /// This method attempts to stop the ECG sensor by interacting with the 
    /// <see cref="MedWandController"/>. It ensures that the action state is 
    /// updated appropriately, even in the event of an exception. The lock 
    /// state of the view is also released in the <c>finally</c> block.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown if an error occurs while stopping the sensor. The exception is 
    /// caught and logged, and the action state is reset to idle.
    /// </exception>
    internal void StopSensor()
    {
        try
        {
            SetAction(ActionState.Disabled);
            _medWandController.StopSensor();
            SetAction(ActionState.Idle);
        }
        catch (Exception outerEx)
        {
            Debug.WriteLine(outerEx.Message);
            SetAction(ActionState.Idle);
        }
        finally
        {
            _setLocked(false);
        }
    }

    /// <summary>
    /// Updates the status message with the provided value and the number of captured readings.
    /// </summary>
    /// <param name="value">The status message to display.</param>
    /// <remarks>
    /// This method formats the status message to include the provided status value and the count of captured readings.
    /// It is used to reflect the current state of the ECG monitoring process.
    /// </remarks>
    private void SetStatus(string value)
    {
        StatusMessage = $"{value}  [{_captured} Captured]";
    }

    /// <summary>
    /// Updates the current action state of the ECG ViewModel and adjusts the UI and locking behavior accordingly.
    /// </summary>
    /// <param name="actionState">The new action state to set. This determines the behavior of the action button and locking mechanism.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="actionState"/> is not a valid <see cref="ActionState"/> value.
    /// </exception>
    /// <remarks>
    /// This method modifies the action button's state, text, and tag based on the provided <paramref name="actionState"/>.
    /// It also updates the locking mechanism and the status message to reflect the current state of the ECG sensor.
    /// </remarks>
    private void SetAction(ActionState actionState)
    {
        switch (actionState)
        {
            case ActionState.Idle:
                SetActionButtonIdle();
                _setLocked(false);
                break;

            case ActionState.Busy:
                _setLocked(true);
                SetActionButtonBusy();
                break;

            case ActionState.Disabled:
                SetActionButtonDisabled();
                _setLocked(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(actionState), actionState, null);
        }

        SetStatus(_medWandController.ReadingState.ToString());
    }

    /// <summary>
    /// Updates the state of the action button to indicate that it is busy.
    /// </summary>
    /// <remarks>
    /// This method sets the action button's state to <see cref="ActionState.Busy"/>, updates the button's 
    /// text to "Stop Recording", and assigns the corresponding tag for the busy state.
    /// </remarks>
    private void SetActionButtonBusy()
    {
        ButtonActionState = ActionState.Busy;
        ButtonActionText = "Stop Recording";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    /// <summary>
    /// Configures the action button to its idle state.
    /// </summary>
    /// <remarks>
    /// This method sets the action button's state to <see cref="ActionState.Idle"/>, updates its 
    /// display text to "Start Recording", and assigns the corresponding tag for the idle state.
    /// </remarks>
    private void SetActionButtonIdle()
    {
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start Recording";
        ButtonActionTag = nameof(ActionState.Idle);
    }

    /// <summary>
    /// Configures the action button to a disabled state.
    /// </summary>
    /// <remarks>
    /// This method updates the action button's state, text, and tag to reflect that it is disabled.
    /// It is typically used when the ECG functionality is not available or cannot be interacted with.
    /// </remarks>
    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = string.Empty;
        ButtonActionTag = nameof(ActionState.Disabled);
    }

    /// <summary>
    /// Initiates the ECG recording process by starting the ECG sensor and updating the action state.
    /// </summary>
    /// <remarks>
    /// This method disables the action button, starts the recording process through the 
    /// <see cref="MedWandController"/>, and then updates the action state to indicate that the 
    /// recording is in progress. It ensures that the UI and underlying system remain synchronized 
    /// during the start of the ECG capture.
    /// </remarks>
    internal void StartCapture()
    {
        SetAction(ActionState.Disabled);
        _medWandController.StartRecording();
        SetAction(ActionState.Busy);
    }

    /// <summary>
    /// Stops the ECG recording process and updates the action state accordingly.
    /// </summary>
    /// <remarks>
    /// This method transitions the <see cref="ActionState"/> to <see cref="ActionState.Disabled"/> 
    /// before stopping the recording via the <see cref="MedWandController"/>. After the recording 
    /// is stopped, it sets the action state back to <see cref="ActionState.Idle"/>.
    /// </remarks>
    internal void StopCapture()
    {
        SetAction(ActionState.Disabled);
        _medWandController.StopRecording();
        SetAction(ActionState.Idle);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="EcgViewModel"/> instance.
    /// </summary>
    /// <remarks>
    /// This method is used to clean up any resources, such as event handlers or 
    /// unmanaged resources, that the <see cref="EcgViewModel"/> may be holding. 
    /// It should be called when the ViewModel is no longer needed to ensure proper 
    /// resource management and avoid memory leaks.
    /// </remarks>
    public void Dispose()
    {
    }

    #region Events

    /// <summary>
    /// Handles the event triggered when a recorded ECG strip is ready.
    /// </summary>
    /// <param name="sender">The source of the event, typically the ECG sensor.</param>
    /// <param name="bytes">The byte array containing the recorded ECG strip data.</param>
    /// <remarks>
    /// This method processes the recorded ECG strip data by appending it to a file 
    /// and updating the internal capture count. The cursor is temporarily set to 
    /// a wait state during the operation.
    /// </remarks>
    private void Ecg_RecordedStripReady(object? sender, byte[] bytes)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        File.AppendAllText("captures.txt", $"[{DateTime.UtcNow:O}] -> {_medWandController.EcgBmpFromCapture(bytes)}\n");
        _captured++;
        Mouse.OverrideCursor = null;
    }

    #endregion


    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;
    private void RaisePropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _statusMessage = "Starting";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            RaisePropertyChanged();
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
            RaisePropertyChanged();
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
            RaisePropertyChanged();
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
            RaisePropertyChanged();
        }
    }

    #endregion

}