using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Core.Extensions;
using SampleWpfApp.Interfaces;
using static MWSDK.NetCore.Internal.CameraHelper;

namespace SampleWpfApp.Views;

public partial class CameraView : ISensorView
{
    public MedWandSensor MedWandSensor => MedWandSensor.Otoscope;

    public event Action<bool>? ViewLockStateChanged;

    public CameraView(MedWandController medWandController)
    {
        InitializeComponent();
        _viewModel = new CameraViewModel(medWandController, VideoPreview);
        _viewModel.CameraModeChanged += OnCameraModeChanged;

        DataContext = _viewModel;

        Focusable = true;
        KeyDown += OnView_KeyDown;
        Loaded += (_, _) => Keyboard.Focus(this);
        Unloaded += OnViewUnloaded;
    }

    private readonly CameraViewModel _viewModel;

    public void Activate()
    {
        _viewModel.Activate();
    }

    public void Deactivate()
    {
        _viewModel.Deactivate();
    }

    public void Dispose()
    {
        this.KeyDown -= OnView_KeyDown;
        this.Unloaded -= OnViewUnloaded;

        _viewModel.CameraModeChanged -= OnCameraModeChanged;
        _viewModel.Deactivate();
        _viewModel.Dispose();
    }

    private void UpdateModeButtons()
    {
        this.SafeInvoke(() =>
        {
            foreach (var child in ModeButtons.Children.OfType<Button>())
            {
                var button = child;
                var modeForButton = button.Tag switch
                {
                    "BtnOff" => CameraModes.Off,
                    "BtnDermatoscope" => CameraModes.Dermatoscope,
                    "BtnOtoscope" => CameraModes.Otoscope,
                    _ => CameraModes.Off
                };

                if (modeForButton == _viewModel.CameraMode)
                {
                    button.BorderBrush = Brushes.Red;
                    button.Background = Brushes.White;
                }
                else
                {
                    button.BorderBrush = null;
                    button.Background = Brushes.White;
                }
            }
        });
    }

    private void OnViewUnloaded(object sender, RoutedEventArgs e)
    {

    }

    private void OnView_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel.KeyDown(e.Key)) e.Handled = true;
    }

    private void ButtonAction_Click(object sender, RoutedEventArgs e)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        _viewModel.CaptureFrame();
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var mode = button.Tag switch
        {
            "BtnOff" => CameraModes.Off,
            "BtnDermatoscope" => CameraModes.Dermatoscope,
            "BtnOtoscope" => CameraModes.Otoscope,
            _ => CameraModes.Off
        };
        _viewModel.SetCameraMode(mode);
    }

    private void OnCameraModeChanged(CameraModes obj)
    {
        UpdateModeButtons();
    }

    public void OnReadingStateChanged(ReadingState readingState)
    {
    }

    public void OnReadingReceived(MedWandReading reading)
    {
    }

    public void OnDeviceError(MedWandDeviceError? error)
    {
    }
}
