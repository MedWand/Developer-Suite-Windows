using MWSDK.NetCore;

namespace SampleWpfApp.Interfaces;

/// <summary>
/// Represents a view interface for sensor-related operations in the application.
/// </summary>
/// <remarks>
/// This interface defines the contract for views that interact with MedWand sensors, 
/// including activation, deactivation, handling sensor readings, and managing device errors.
/// </remarks>
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
