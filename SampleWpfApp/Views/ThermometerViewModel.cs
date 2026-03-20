using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Core;

namespace SampleWpfApp.Views;

public sealed class ThermometerViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MedWandController _controller;
    private readonly Action<bool> _setLocked;
    private MedWandReading? _reading;

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

    public void Initialize()
    {
        _reading = new MedWandReading();
        UpdateReadingText();
        SetAction(ActionState.Idle);
    }

    public void OnReadingStateChanged(ReadingState state)
    {
        SetStatus(state.ToString());
    }

    public void OnReadingReceived(MedWandReading reading)
    {
        _reading = reading;
        UpdateReadingText();
    }

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

    private void UpdateReadingText()
    {
        if (_reading == null)
            return;

        string FormatTemp(string raw) =>
            string.IsNullOrEmpty(raw) || raw == "Reading" ? "--" : $"{raw} F";

        TempObject = $"{FormatTemp(_reading.TempObject ?? string.Empty)}";
    }

    private void SetStatus(string value)
    {
        StatusMessage = value;
    }

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

    private void SetActionButtonBusy()
    {
        ButtonActionState = ActionState.Busy;
        ButtonActionText = "Stop";
        ButtonActionTag = nameof(ActionState.Busy);
    }

    private void SetActionButtonIdle()
    {
        ButtonActionState = ActionState.Idle;
        ButtonActionText = "Start";
        ButtonActionTag = nameof(ActionState.Idle);
    }

    private void SetActionButtonDisabled()
    {
        ButtonActionState = ActionState.Disabled;
        ButtonActionText = string.Empty;
        ButtonActionTag = nameof(ActionState.Disabled);
    }

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
