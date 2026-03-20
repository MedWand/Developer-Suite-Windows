using MWSDK.NetCore;

namespace SampleWpfApp.Interfaces;

public interface ISensorView : IDisposable
{
    MedWandSensor MedWandSensor { get; }

    event Action<bool>? ViewLockStateChanged;

    void Activate();

    void Deactivate();

    void OnReadingStateChanged(ReadingState readingState);

    void OnReadingReceived(MedWandReading reading);

    void OnDeviceError(MedWandDeviceError? error);
}
