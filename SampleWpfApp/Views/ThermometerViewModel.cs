using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Core;

namespace SampleWpfApp.Views;

/// <summary>
/// Represents the ViewModel for the thermometer sensor in the WPF application.
/// </summary>
/// <remarks>
/// This class manages the state and behavior of the thermometer sensor, including handling
/// sensor readings, updating the UI, and responding to user actions. It implements the
/// <see cref="INotifyPropertyChanged"/> interface to support data binding and the
/// <see cref="IDisposable"/> interface for resource cleanup.
/// </remarks>
public sealed class ThermometerViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MedWandController _controller;
    private readonly Action<bool> _setLocked;
    private MedWandReading? _reading;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThermometerViewModel"/> class.
    /// </summary>
    /// <param name="controller">
    /// The <see cref="MedWandController"/> instance used to interact with the thermometer sensor.
    /// </param>
    /// <param name="setLocked">
    /// An <see cref="Action{T}"/> delegate that sets the lock state of the view.
    /// </param>
    /// <remarks>
    /// This constructor sets up the initial state of the ViewModel, including initializing
    /// status messages, button states, and temperature display.
    /// </remarks>
    public ThermometerViewModel(MedWandController controller, Action<bool> setLocked)
    {
        _controller = controller;
        _setLocked = setLocked;

        StatusMessage = "Starting";
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start";
        ButtonActionTag = nameof(ActionState.Idle);
        TempObject = "--";
    }

    /// <summary>
    /// Initializes the thermometer ViewModel by setting up the initial reading state,
    /// updating the displayed reading text, and configuring the default action state.
    /// </summary>
    /// <remarks>
    /// This method prepares the ViewModel for use by initializing the internal reading object,
    /// updating the UI with the initial reading, and setting the action state to <see cref="ActionState.Idle"/>.
    /// It should be called when the ViewModel is first loaded or reset.
    /// </remarks>
    public void Initialize()
    {
        _reading = new MedWandReading();
        UpdateReadingText();
        SetAction(ActionState.Idle);
    }

    /// <summary>
    /// Handles changes in the reading state of the thermometer sensor.
    /// </summary>
    /// <param name="state">The new <see cref="ReadingState"/> of the thermometer sensor.</param>
    /// <remarks>
    /// This method updates the status message to reflect the current state of the thermometer sensor.
    /// It is typically called when the sensor's reading state changes.
    /// </remarks>
    public void OnReadingStateChanged(ReadingState state)
    {
        SetStatus(state.ToString());
    }

    /// <summary>
    /// Handles the event when a new thermometer reading is received.
    /// </summary>
    /// <param name="reading">
    /// The <see cref="MedWandReading"/> instance containing the updated thermometer data.
    /// </param>
    /// <remarks>
    /// This method updates the internal reading state with the provided data and refreshes
    /// the displayed reading text in the UI. It ensures that the ViewModel reflects the
    /// latest thermometer reading.
    /// </remarks>
    public void OnReadingReceived(MedWandReading reading)
    {
        _reading = reading;
        UpdateReadingText();
    }

    /// <summary>
    /// Handles device error events by updating the action state of the ViewModel.
    /// </summary>
    /// <param name="error">
    /// The <see cref="MedWandDeviceError"/> instance representing the error that occurred, 
    /// or <c>null</c> if there is no specific error.
    /// </param>
    /// <remarks>
    /// This method adjusts the action state of the ViewModel based on the presence or absence
    /// of a device error. If <paramref name="error"/> is <c>null</c>, the action state is set
    /// to <see cref="ActionState.Idle"/>. Otherwise, it is set to <see cref="ActionState.Disabled"/>.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown if an unexpected error occurs while updating the action state.
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
    /// Handles the click event of the action button in the thermometer view.
    /// </summary>
    /// <remarks>
    /// This method determines the current state of the action button and performs the appropriate action:
    /// <list type="bullet">
    /// <item><description>Starts the sensor if the button is in the <see cref="ActionState.Idle"/> state.</description></item>
    /// <item><description>Stops the sensor if the button is in the <see cref="ActionState.Busy"/> state.</description></item>
    /// <item><description>Does nothing if the button is in the <see cref="ActionState.Disabled"/> state.</description></item>
    /// </list>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if the button state is invalid.
    /// </remarks>
    public void OnActionButtonClick()
    {
        switch (ButtonActionState)
        {
            case ActionState.Idle:
                StartSensor();
                break;
            case ActionState.Busy:
                StopSensor(fromTimeout: false);
                break;
            case ActionState.Disabled:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Starts the thermometer sensor and updates the ViewModel state accordingly.
    /// </summary>
    /// <remarks>
    /// This method attempts to start the thermometer sensor by interacting with the 
    /// <see cref="MedWandController"/>. If the sensor starts successfully, the action state 
    /// is set to <see cref="ActionState.Busy"/>. Otherwise, the action state is reverted to 
    /// <see cref="ActionState.Idle"/>. In case of an exception, the action state is set to 
    /// <see cref="ActionState.Disabled"/>.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown if an error occurs while attempting to start the thermometer sensor.
    /// </exception>
    private void StartSensor()
    {
        try
        {
            SetAction(ActionState.Disabled);
            _setLocked(true);

            if (_controller.StartThermometer())
            {
                SetAction(ActionState.Busy);
            }
            else
            {
                SetAction(ActionState.Idle);
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
    /// Stops the thermometer sensor and updates the ViewModel state accordingly.
    /// </summary>
    /// <param name="fromTimeout">
    /// A boolean value indicating whether the sensor stop operation was triggered by a timeout.
    /// If <c>true</c>, the sensor is stopped due to a timeout; otherwise, it is stopped manually.
    /// </param>
    /// <remarks>
    /// This method ensures that the sensor is stopped safely and the ViewModel state is reset.
    /// It also handles any exceptions that may occur during the stop operation and ensures
    /// that the lock state of the view is released in the <c>finally</c> block.
    /// </remarks>
    /// <exception cref="Exception">
    /// Logs any exceptions that occur during the sensor stop operation.
    /// </exception>
    private void StopSensor(bool fromTimeout)
    {
        try
        {
            SetAction(ActionState.Disabled);

            if (!fromTimeout)
            {
                _controller.StopSensor();
            }

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
    /// Updates the displayed temperature reading text based on the current thermometer reading.
    /// </summary>
    /// <remarks>
    /// This method formats the temperature reading from the <see cref="MedWandReading"/> object
    /// and updates the <see cref="TempObject"/> property. If the reading is unavailable or invalid,
    /// it sets the temperature display to a default placeholder value ("--").
    /// </remarks>
    private void UpdateReadingText()
    {
        if (_reading == null)
            return;

        string FormatTemp(string raw) =>
            string.IsNullOrEmpty(raw) || raw == "Reading" ? "--" : $"{raw} F";

        TempObject = $"{FormatTemp(_reading.TempObject ?? string.Empty)}";
    }

    /// <summary>
    /// Updates the status message of the thermometer ViewModel.
    /// </summary>
    /// <param name="value">The new status message to be displayed.</param>
    /// <remarks>
    /// This method sets the <see cref="StatusMessage"/> property to the specified value,
    /// which is typically used to communicate the current state or progress of the thermometer sensor.
    /// </remarks>
    private void SetStatus(string value)
    {
        StatusMessage = value;
    }

    /// <summary>
    /// Sets the current action state of the thermometer ViewModel and updates the UI accordingly.
    /// </summary>
    /// <param name="actionState">The new <see cref="ActionState"/> to be applied.</param>
    /// <remarks>
    /// This method adjusts the behavior and appearance of the action button based on the specified
    /// <paramref name="actionState"/>. It also updates the lock state and the status message
    /// to reflect the current state of the thermometer sensor.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="actionState"/> is not a valid <see cref="ActionState"/> value.
    /// </exception>
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

        SetStatus(_controller.ReadingState.ToString());
    }

    /// <summary>
    /// Configures the action button to reflect a "Busy" state.
    /// </summary>
    /// <remarks>
    /// This method updates the action button's state, text, and tag to indicate that
    /// an ongoing operation is in progress. It is typically used when the thermometer
    /// sensor is performing a task that requires user interaction to be temporarily disabled.
    /// </remarks>
    private void SetActionButtonBusy()
    {
        ButtonActionState = ActionState.Busy;
        ButtonActionText = "Stop";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    /// <summary>
    /// Configures the action button to represent the idle state.
    /// </summary>
    /// <remarks>
    /// This method sets the action button's state, text, and tag to indicate that it is in the idle state.
    /// It is typically used when the thermometer is not actively performing any operation.
    /// </remarks>
    private void SetActionButtonIdle()
    {
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start";
        ButtonActionTag = nameof(ActionState.Idle);
    }

    /// <summary>
    /// Configures the action button to a disabled state.
    /// </summary>
    /// <remarks>
    /// This method updates the button's state, text, and tag to reflect that it is disabled.
    /// It is typically used when the action associated with the button cannot be performed.
    /// </remarks>
    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = string.Empty;
        ButtonActionTag = nameof(ActionState.Disabled);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="ThermometerViewModel"/> instance.
    /// </summary>
    /// <remarks>
    /// This method is called to perform cleanup operations, such as releasing unmanaged resources
    /// or unsubscribing from events, to prevent memory leaks. After calling this method, the
    /// <see cref="ThermometerViewModel"/> instance should no longer be used.
    /// </remarks>
    public void Dispose()
    {
    }

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

    private string _buttonActionText = "Start";
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

    private string _tempObject = "--";
    public string TempObject
    {
        get => _tempObject;
        set
        {
            if (_tempObject == value) return;
            _tempObject = value;
            RaisePropertyChanged();
        }
    }

    #endregion
}
