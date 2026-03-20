using System.Windows;
using System.Windows.Controls;
using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Interfaces;

namespace SampleWpfApp.Views;

public partial class PulseOximeterView : Page, ISensorView
{
    private readonly MedWandController _medWandController;
    private readonly PulseOximeterViewModel _viewModel;

    public PulseOximeterView(MedWandController medWandController)
    {
        InitializeComponent();

        _medWandController = medWandController;

        _viewModel = new PulseOximeterViewModel(
            medWandController,
            locked => ViewLockStateChanged?.Invoke(locked));

        DataContext = _viewModel;

        Loaded += PulseOximeterView_Loaded;
        Unloaded += PulseOximeterView_Unloaded;
    }

    private void PulseOximeterView_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();
    }

    private void PulseOximeterView_Unloaded(object sender, RoutedEventArgs e)
    {
    }

    public MedWandSensor MedWandSensor => MedWandSensor.PulseOximeter;

    public event Action<bool>? ViewLockStateChanged;

    public void Activate()
    {
    }

    public void Deactivate()
    {
    }

    public void OnReadingStateChanged(ReadingState readingState)
        => _viewModel.OnReadingStateChanged(readingState);

    public void OnReadingReceived(MedWandReading reading)
        => _viewModel.OnReadingReceived(reading);

    public void OnDeviceError(MedWandDeviceError? error)
        => _viewModel.OnDeviceError(error);

    public void Dispose()
    {
        Loaded -= PulseOximeterView_Loaded;
        Unloaded -= PulseOximeterView_Unloaded;
        _viewModel.Dispose();
    }

    private void ButtonAction_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OnActionButtonClick();
    }

}
