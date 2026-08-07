using MWSDK.NetCore;
using MWSDK.Wpf;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using SampleWpfApp.Core;

namespace SampleWpfApp.Views;

/// <summary>
/// Represents the ViewModel for the Pulse Oximeter sensor in the WPF application.
/// </summary>
/// <remarks>
/// This class manages the state and behavior of the Pulse Oximeter sensor, including
/// initializing the sensor, handling readings, updating UI-bound properties, and managing
/// the sensor's lifecycle. It implements <see cref="INotifyPropertyChanged"/> to support
/// data binding and <see cref="IDisposable"/> for proper resource management.
/// </remarks>
public sealed class PulseOximeterViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MedWandController _controller;
    private readonly Action<bool> _setLocked;
    private MedWandReading? _reading;

    /// <summary>
    /// Initializes a new instance of the <see cref="PulseOximeterViewModel"/> class.
    /// </summary>
    /// <param name="controller">
    /// An instance of <see cref="MedWandController"/> used to manage the Pulse Oximeter sensor.
    /// </param>
    /// <param name="setLocked">
    /// An <see cref="Action{T}"/> delegate to handle the locked state of the view.
    /// </param>
    /// <remarks>
    /// This constructor sets up the initial state of the ViewModel, including default values for
    /// UI-bound properties such as <see cref="StatusMessage"/>, <see cref="ButtonActionState"/>,
    /// <see cref="ButtonActionText"/>, and <see cref="ButtonActionTag"/>.
    /// </remarks>
    public PulseOximeterViewModel(
        MedWandController controller,
        Action<bool> setLocked)
    {
        _controller = controller;
        _setLocked = setLocked;

        StatusMessage = "Starting";
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start";
        ButtonActionTag = nameof(ActionState.Idle);
        SpO2 = "SpO2 : --";
        PulseRate = "Pulse Rate : --";
    }

    /// <summary>
    /// Initializes the Pulse Oximeter sensor and sets its initial state.
    /// </summary>
    /// <remarks>
    /// This method prepares the Pulse Oximeter sensor for operation by creating a new reading instance,
    /// updating the reading text, and setting the initial action state to <see cref="ActionState.Idle"/>.
    /// It ensures the sensor is ready for interaction and updates the UI accordingly.
    /// </remarks>
    public void Initialize()
    {
        _reading = new MedWandReading
        {
            TimeStamp = DateTime.UtcNow,
            Status = string.Empty,
            Index = 1,
            Count = 0,
            SensorType = nameof(MedWandSensor.PulseOximeter),
            TempAmbient = string.Empty,
            TempObject = string.Empty,
            PulseRate = null,
            Spo2 = null,
            EcgData = null
        };

        UpdateReadingText();
        SetAction(ActionState.Idle);
    }

    /// <summary>
    /// Handles changes in the reading state of the Pulse Oximeter sensor.
    /// </summary>
    /// <param name="state">
    /// The new <see cref="ReadingState"/> of the sensor.
    /// </param>
    /// <remarks>
    /// This method updates the status message to reflect the current reading state.
    /// It is typically invoked when the sensor's state changes, ensuring the UI
    /// remains synchronized with the sensor's operational status.
    /// </remarks>
    public void OnReadingStateChanged(ReadingState state)
    {
        SetStatus(state.ToString());
    }

    /// <summary>
    /// Handles the event when a new reading is received from the Pulse Oximeter sensor.
    /// </summary>
    /// <param name="reading">
    /// The <see cref="MedWandReading"/> object containing the latest sensor data.
    /// </param>
    /// <remarks>
    /// This method updates the internal state of the ViewModel with the provided reading
    /// and refreshes the UI-bound properties to reflect the new data.
    /// </remarks>
    public void OnReadingReceived(MedWandReading reading)
    {
        _reading = reading;
        UpdateReadingText();
    }

    /// <summary>
    /// Handles device error events for the Pulse Oximeter sensor.
    /// </summary>
    /// <param name="error">
    /// An optional <see cref="MedWandDeviceError"/> instance representing the error encountered by the device.
    /// If <c>null</c>, the device is assumed to be in a normal state.
    /// </param>
    /// <remarks>
    /// This method updates the action state of the ViewModel based on the presence or absence of a device error.
    /// If an error is present, the action state is set to <see cref="ActionState.Disabled"/>; otherwise, it is set to <see cref="ActionState.Idle"/>.
    /// </remarks>
    /// <exception cref="Exception">
    /// Captures and logs any exceptions that occur during the state update process.
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
    /// Handles the click event of the action button in the Pulse Oximeter view.
    /// </summary>
    /// <remarks>
    /// This method changes the state of the Pulse Oximeter sensor based on the current
    /// <see cref="ButtonActionState"/>. It starts the sensor if the state is <see cref="ActionState.Idle"/>,
    /// stops the sensor if the state is <see cref="ActionState.Busy"/>, and performs no action if the state
    /// is <see cref="ActionState.Disabled"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <see cref="ButtonActionState"/> is not a valid <see cref="ActionState"/> value.
    /// </exception>
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
    /// Starts the Pulse Oximeter sensor and updates the ViewModel's state accordingly.
    /// </summary>
    /// <remarks>
    /// This method attempts to initialize the Pulse Oximeter sensor using the associated
    /// <see cref="MedWandController"/>. If the initialization is successful, the action state
    /// is updated to <see cref="ActionState.Busy"/>. Otherwise, it reverts to <see cref="ActionState.Idle"/>.
    /// In case of an exception, the action state is set to <see cref="ActionState.Disabled"/>,
    /// and the locked state is released.
    /// </remarks>
    /// <exception cref="Exception">
    /// Thrown if an error occurs during the initialization of the Pulse Oximeter sensor.
    /// The exception is logged but not propagated further.
    /// </exception>
    private void StartSensor()
    {
        try
        {
            SetAction(ActionState.Disabled);
            _setLocked(true);

            if (_controller.StartPulseOximeter())
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
    /// Stops the Pulse Oximeter sensor and updates the state of the ViewModel.
    /// </summary>
    /// <param name="fromTimeout">
    /// A boolean value indicating whether the stop operation was triggered by a timeout.
    /// </param>
    /// <remarks>
    /// This method disables the action state, stops the sensor if the operation is not
    /// triggered by a timeout, and then resets the action state to idle. It ensures that
    /// the locked state of the view is properly managed and handles any exceptions that
    /// occur during the stop operation.
    /// </remarks>
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
    /// Updates the text representation of the SpO2 and Pulse Rate readings.
    /// </summary>
    /// <remarks>
    /// This method formats and assigns the current SpO2 and Pulse Rate values from the
    /// <see cref="MedWandReading"/> instance to the <see cref="SpO2"/> and <see cref="PulseRate"/> 
    /// properties, respectively. If no reading is available, the method exits without making changes.
    /// </remarks>
    private void UpdateReadingText()
    {
        if (_reading == null)
            return;
        SpO2 = $"SpO2 : {_reading.Spo2}";
        PulseRate = $"PulseRate : {_reading.PulseRate}";
    }

    /// <summary>
    /// Updates the status message displayed in the UI.
    /// </summary>
    /// <param name="value">
    /// The new status message to be set.
    /// </param>
    /// <remarks>
    /// This method is used to update the <see cref="StatusMessage"/> property, 
    /// which is bound to the UI to reflect the current state or status of the Pulse Oximeter.
    /// </remarks>
    private void SetStatus(string value)
    {
        StatusMessage = value;
    }

    /// <summary>
    /// Updates the current action state of the Pulse Oximeter and adjusts the UI and lock state accordingly.
    /// </summary>
    /// <param name="actionState">The new action state to set. Must be one of the values defined in <see cref="ActionState"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the provided <paramref name="actionState"/> is not a valid <see cref="ActionState"/> value.
    /// </exception>
    /// <remarks>
    /// This method modifies the button state, updates the lock state, and sets the status message
    /// based on the provided <paramref name="actionState"/>. It ensures the UI reflects the current
    /// operational state of the Pulse Oximeter.
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

        SetStatus(_controller.ReadingState.ToString());
    }

    /// <summary>
    /// Configures the action button to reflect a "Busy" state.
    /// </summary>
    /// <remarks>
    /// This method updates the properties <see cref="ButtonActionState"/>, 
    /// <see cref="ButtonActionText"/>, and <see cref="ButtonActionTag"/> to indicate 
    /// that the action button is in a "Busy" state. Typically used when the Pulse Oximeter 
    /// is performing an ongoing operation.
    /// </remarks>
    private void SetActionButtonBusy()
    {
        ButtonActionState = ActionState.Busy;
        ButtonActionText = "Stop";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    /// <summary>
    /// Configures the action button to the idle state.
    /// </summary>
    /// <remarks>
    /// This method sets the <see cref="ButtonActionState"/> to <see cref="ActionState.Idle"/>,
    /// updates the <see cref="ButtonActionText"/> to "Start", and assigns the
    /// <see cref="ButtonActionTag"/> to the name of the idle state.
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
    /// This method updates the <see cref="ButtonActionState"/> to <see cref="ActionState.Disabled"/>,
    /// clears the <see cref="ButtonActionText"/>, and sets the <see cref="ButtonActionTag"/> to
    /// represent the disabled state.
    /// </remarks>
    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = string.Empty;
        ButtonActionTag = nameof(ActionState.Disabled);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="PulseOximeterViewModel"/> instance.
    /// </summary>
    /// <remarks>
    /// This method is called to clean up any resources, such as event handlers or unmanaged resources,
    /// that the ViewModel may be holding. It ensures proper disposal of resources to prevent memory leaks.
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

    private string _spO2 = "--";
    public string SpO2
    {
        get => _spO2;
        set
        {
            if (_spO2 == value) return;
            _spO2 = value;
            RaisePropertyChanged();
        }
    }

    private string _pulseRate = "--";
    public string PulseRate
    {
        get => _pulseRate;
        set
        {
            if (_pulseRate == value) return;
            _pulseRate = value;
            RaisePropertyChanged();
        }
    }
    #endregion

}