using System.Windows;
using System.Windows.Controls;
using MWSDK.NetCore;
using MWSDK.Wpf;
using SampleWpfApp.Interfaces;

namespace SampleWpfApp.Views;

public partial class ThermometerView : Page, ISensorView
{
    private readonly MedWandController _medWandController;
    private readonly ThermometerViewModel _viewModel;

    public ThermometerView(MedWandController medWandController)
    {
        InitializeComponent();

        _medWandController = medWandController;

        _viewModel = new ThermometerViewModel(
            medWandController,
            locked => ViewLockStateChanged?.Invoke(locked));

        DataContext = _viewModel;

        Loaded += ThermometerView_Loaded;
        Unloaded += ThermometerView_Unloaded;
    }

    private void ThermometerView_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.Initialize();
    }

    private void ThermometerView_Unloaded(object sender, RoutedEventArgs e)
    {
    }

    public MedWandSensor MedWandSensor => MedWandSensor.Thermometer;

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
        Loaded -= ThermometerView_Loaded;
        Unloaded -= ThermometerView_Unloaded;
        _viewModel.Dispose();
    }

    private void ButtonAction_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OnActionButtonClick();
    }
}
