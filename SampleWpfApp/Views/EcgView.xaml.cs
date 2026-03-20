using System.Windows;
using System.Windows.Controls;
using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Interfaces;

namespace SampleWpfApp.Views;

public partial class EcgView : Page, ISensorView
{
    private readonly MedWandController _medWandController;
    private readonly EcgViewModel _viewModel;

    public MedWandSensor MedWandSensor => MedWandSensor.Ecg;

    public event Action<bool>? ViewLockStateChanged;

    public EcgView(MedWandController medWandController)
    {
        InitializeComponent();

        _medWandController = medWandController;

        _viewModel = new EcgViewModel(
            medWandController,
            locked => ViewLockStateChanged?.Invoke(locked));

        DataContext = _viewModel;
    }

    public void Activate()
    {
        _viewModel.Activate();
        _viewModel.StartSensor();
    }

    public void Deactivate()
    {
        _viewModel.Deactivate();
    }

    public void OnReadingStateChanged(ReadingState readingState)
        => _viewModel.OnReadingStateChanged(readingState);

    public void OnReadingReceived(MedWandReading reading)
        => _viewModel.OnReadingReceived(reading);

    public void OnDeviceError(MedWandDeviceError? error)
        => _viewModel.OnDeviceError(error);

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    private void ButtonAction_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OnActionButtonClick();
    }
}
