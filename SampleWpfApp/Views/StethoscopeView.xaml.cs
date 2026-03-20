using MWSDK.NetCore;
using MWSDK.NetCore.Internal;
using MWSDK.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SampleWpfApp.Core;
using SampleWpfApp.Core.Extensions;
using SampleWpfApp.Interfaces;
using static MWSDK.NetCore.Internal.StethoscopeHelpers;

namespace SampleWpfApp.Views;

public partial class StethoscopeView : ISensorView
{

    public MedWandSensor MedWandSensor => MedWandSensor.Otoscope;

    public event Action<bool>? ViewLockStateChanged;

    public StethoscopeView(MedWandController medWandController)
    {
        InitializeComponent();
        _viewModel = new StethoscopeViewModel(medWandController);
        _viewModel.StethoscopeModeChanged += OnStethoscopeModeChanged;

        DataContext = _viewModel;

        Focusable = true;
        Loaded += (_, _) => Keyboard.Focus(this);
        Unloaded += OnViewUnloaded;
    }

    private readonly StethoscopeViewModel _viewModel;

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
        this.Unloaded -= OnViewUnloaded;

        _viewModel.StethoscopeModeChanged -= OnStethoscopeModeChanged;
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
                    "BtnOff" => MicrophoneModes.Off,
                    "BtnHeart" => MicrophoneModes.Heart,
                    "BtnLungs" => MicrophoneModes.Lungs,
                    "BtnBowel" => MicrophoneModes.Bowel,
                    _ => MicrophoneModes.Off
                };

                if (modeForButton == _viewModel.StethoscopeMode)
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

    private void ButtonAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        Mouse.OverrideCursor = Cursors.Wait;

        switch (button.Tag)
        {
            case nameof(ActionState.Idle):
                _viewModel.StartCapture();
                break;
            case nameof(ActionState.Busy):
                _viewModel.StopCapture();
                break;
        }

        Mouse.OverrideCursor = null;
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var mode = button.Tag switch
        {
            "BtnOff" => MicrophoneModes.Off,
            "BtnHeart" => MicrophoneModes.Heart,
            "BtnLungs" => MicrophoneModes.Lungs,
            "BtnBowel" => MicrophoneModes.Bowel,
            _ => MicrophoneModes.Off
        };
        _viewModel.SetStethoscopeMode(mode);
    }

    private void OnStethoscopeModeChanged(StethoscopeHelpers.MicrophoneModes obj)
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
