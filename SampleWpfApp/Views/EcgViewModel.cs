using MWSDK.NetCore;
using MWSDK.Wpf;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SampleWpfApp.Core;

namespace SampleWpfApp.Views;

public sealed class EcgViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MedWandController _medWandController;
    private readonly Action<bool> _setLocked;
    private MedWandReading? _reading;
    private bool _isActivated;
    private int _captured;

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

    internal void Deactivate()
    {
        StopSensor();
        if (_medWandController.Ecg == null) return;
        _medWandController.Ecg.RecordedStripReady -= Ecg_RecordedStripReady;
        SetStatus("Not Monitoring");
    }

    public void OnReadingStateChanged(ReadingState state)
    {
        SetStatus(state.ToString());
    }

    public void OnReadingReceived(MedWandReading reading)
    {
        _reading = reading;
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

    private void SetStatus(string value)
    {
        StatusMessage = $"{value}  [{_captured} Captured]";
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

        SetStatus(_medWandController.ReadingState.ToString());
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
        ButtonActionText = string.Empty;
        ButtonActionTag = nameof(ActionState.Disabled);
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
    }

    #region Events

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