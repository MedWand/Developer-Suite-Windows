# MedWand DECL Windows WPF Sample

Windows Presentation Foundation (WPF) sample for the MedWand Device Encryption and Communication Library (DECL). The application demonstrates using `MedWandController` for device initialization, temperature, pulse oximetry, ECG, stethoscope, and camera workflows, and `FirmwareController` for firmware updates.

This README describes the SDK calls **used by `SampleWpfApp`**, not every API exposed by the SDK packages.

## Requirements

- A Windows development environment with the .NET 8 SDK and support for the project's `net8.0-windows10.0.26100.0` target framework.
- For Visual Studio, the .NET desktop development workload. The repository contains a Visual Studio solution at `SampleWpfApp/SampleWpfApp.sln`.
- A MedWand device and a MedWand SDK license and public key.
- Access to NuGet to restore the dependencies declared in `SampleWpfApp/SampleWpfApp.csproj`.

The sample references `MWSDK.Wpf` version `3.0.2.6`. It also references `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Logging.Debug`, `System.Drawing.Common`, and `NAudio`.

## Setup and run

1. Clone or download [Developer-Suite-Windows](https://github.com/MedWand/Developer-Suite-Windows).
2. Open [`SampleWpfApp/App.xaml.cs`](SampleWpfApp/App.xaml.cs) and set the two license fields in the `Settings` class to your issued values:

   ```csharp
   public static string MwSdkLicense = @"YOUR_LICENSE";
   public static string MwSdkPublicKey = @"YOUR_PUBLIC_KEY";
   ```

   The checked-in sample contains empty strings for both fields. Do not commit your real credentials to a public repository.

3. Open `SampleWpfApp/SampleWpfApp.sln` in Visual Studio, restore NuGet packages, and run `SampleWpfApp`; alternatively, from the repository root, use:

   ```powershell
   dotnet restore .\SampleWpfApp\SampleWpfApp.sln
   dotnet run --project .\SampleWpfApp\SampleWpfApp.csproj
   ```

4. Connect the MedWand device. The sample attempts to connect and displays a retry/cancel prompt if a device is not found.
5. After initialization, select **Thermometer**, **Pulse Oximeter**, **Stethoscope**, **Camera**, or **ECG** in the application's toolbar.

The main initialization and connection flow is implemented in [`SampleWpfApp/MainWindow.xaml.cs`](SampleWpfApp/MainWindow.xaml.cs).

## `MedWandController` — public MedWand SDK API

### Create, connect, and initialize

`MainWindow` creates a `MedWandController` and performs these operations in order:

1. Subscribes to `OnLicenseError`.
2. Calls `Construct(Settings.MwSdkLicense, Settings.MwSdkPublicKey)`.
3. Calls `Connect()` and checks `IsConnected`.
4. Optionally calls `CheckFirmwareVersion()` according to the firmware-check setting described below.
5. Verifies `IsLicenseValid` and inspects `DeviceState`.
6. Subscribes to device events and calls `Initialize()`.
7. Checks `IsInitialized`, subscribes to reading events, creates the sensor views, and calls `Configure(_ecgView.GridContainer)`.

The sample uses the following device information members when displaying status or device details:

| Members | Usage in `MainWindow` |
| --- | --- |
| `ComPort`, `VendorId`, `ProductId`, `DeviceId`, `Udi` | Connection and device identifiers |
| `Generation`, `FirmwareVersion`, `CameraModel` | Device and firmware information |
| `DeviceState`, `IsConnected`, `IsInitialized`, `IsLicenseValid` | Connection and initialization checks |
| `IsBootloaderMode()` | Bootloader state shown in device information |

### Device and reading events

| Event | Handler parameter in the sample | Usage |
| --- | --- | --- |
| `OnLicenseError` | `LicenseState` | Reports license errors |
| `OnDeviceError` | `MedWandDeviceError` | Passes errors to the active sensor view |
| `OnDeviceStateChanged` | `DeviceState` | Receives device state changes |
| `OnReadingStateChanged` | `ReadingState` | Passes reading state to the active sensor view |
| `OnReadingReceived` | `MedWandReading` | Dispatches thermometer, pulse oximeter, or ECG readings based on `SensorType` |
| `OnLedIntensityChanged` | `int` | Updates the camera view's displayed LED intensity |

The sample marshals device-error and reading-event handling to the WPF UI thread. When closing or replacing the controller, it calls `StopSensor()`, unsubscribes from its events, and calls `Dispose()`.

### Thermometer

The [`ThermometerViewModel`](SampleWpfApp/Views/ThermometerViewModel.cs) calls `StartThermometer()` to begin a measurement and `StopSensor()` when the user stops it. It receives values through `OnReadingReceived` and displays `MedWandReading.TempObject` as a Fahrenheit reading.

### Pulse oximeter

The [`PulseOximeterViewModel`](SampleWpfApp/Views/PulseOximeterViewModel.cs) calls `StartPulseOximeter()` and `StopSensor()`. It processes `MedWandReading` values, including `Spo2` and `PulseRate`, received through the controller's reading event.

### ECG

The [`EcgViewModel`](SampleWpfApp/Views/EcgViewModel.cs) uses:

- `StartEcg()` and `StopSensor()` for ECG monitoring.
- `StartRecording()` and `StopRecording()` for an ECG capture.
- `Ecg.RecordedStripReady` to receive recorded strip bytes.
- `EcgBmpFromCapture(bytes)` to process a recorded strip.

The main window also passes the ECG view's `GridContainer` to `Configure(...)` after creating the sensor views.

### Stethoscope

The [`StethoscopeViewModel`](SampleWpfApp/Views/StethoscopeViewModel.cs) supports **Off**, **Heart**, **Lungs**, and **Bowel** modes. It uses:

- `SetStethoscopeMode(stethoscopeMode, null)` to select a mode using the SDK audio stack.
- `StartRecording()` and `StopRecording()` to control audio capture.
- `Stethoscope.RecordedFramesReady` to receive captured audio bytes.
- `StethoscopeWavFromCapture(bytes)` to process recorded audio.
- `StethoscopeMode` and `StethoscopeModel` to read the selected mode and device model.

The sample uses `NAudio` to select and adjust Windows audio endpoints for volume and microphone gain. A separate single-argument `SetStethoscopeMode(...)` call is present only as a **commented-out alternative**; it is not the active sample implementation.

### Camera: dermatoscope and otoscope

The [`CameraViewModel`](SampleWpfApp/Views/CameraViewModel.cs) uses `SetCameraMode(_videoPreview, cameraMode)` with `CameraModes.Off`, `CameraModes.Dermatoscope`, and `CameraModes.Otoscope`. The view supplies a WPF `Image` control for preview.

Select **Dermatoscope** or **Otoscope** to start preview, **Capture** to request a frame via `StartRecording()`, and **Off** to stop preview. Captured bytes arrive through `Camera.RecordedFrameReady`; the sample passes them to `CameraBmpFromCapture(bytes)`.

Other camera API calls demonstrated by the sample are:

| API | Purpose in the sample |
| --- | --- |
| `CameraSetFocusMode(...)`, `CameraSetFocusValue(...)` | Select and adjust focus |
| `CameraMove(...)` | Move the camera overlay |
| `CameraZoom(...)` | Adjust zoom |
| `CameraRadius(...)` | Adjust the otoscope mask radius |
| `CameraReset()` | Reset camera overlay adjustments |
| `CameraLedIntensityMax`, `OnLedIntensityChanged` | Display supported LED intensity information |
| `CameraHasOnTimer`, `CameraOnTimeMax`, `CameraCoolTimeMin` | Handle the camera's operating/cooldown timer when supported |

The camera view handles these keyboard shortcuts:

| Keys | Sample action |
| --- | --- |
| `A` / `M` | Auto / manual focus |
| Numpad `0` / `1` | Decrease / increase manual focus value |
| Arrow keys | Move the overlay |
| Numpad `+` / `-` | Zoom in / out |
| `Page Up` / `Page Down` | Increase / decrease mask radius |
| `Delete` | Reset adjustments |

These shortcuts are implemented by the WPF sample and are not additional SDK methods.

## `FirmwareController` — public API used by the sample

The firmware workflow is implemented in `MainWindow.MedWandFirmwareProcess(...)` in [`MainWindow.xaml.cs`](SampleWpfApp/MainWindow.xaml.cs).

The sample catches `MedWandFirmwareUpdateRequiredException` and reads its `CurrentVersion` and `RequiredVersion` values. It disposes of the current `MedWandController` before starting the firmware workflow and prompts the user to continue or cancel.

The firmware controller is constructed with a `CancellationToken` and an `ILoggerFactory`:

```csharp
var cancellation = new CancellationTokenSource();
FirmwareController medwandFirmwareController = new(cancellation.Token, _loggerFactory);

medwandFirmwareController.FirmwareStateChanged += MedwandFirmwareStateChanged;
medwandFirmwareController.FirmwareProgressChanged += MedwandFirmwareProgressChanged;
medwandFirmwareController.FirmwareError += MedwandFirmwareError;
```

The sample then awaits `medwandFirmwareController.StartAsync()`, examines its `bool` result, removes the event subscriptions in `finally`, and awaits `medwandFirmwareController.DisposeAsync()`.

| Event | Handler parameter in the sample | Usage |
| --- | --- | --- |
| `FirmwareStateChanged` | `UpdaterState` | Display the current firmware updater state |
| `FirmwareProgressChanged` | `int` | Display firmware progress as a percentage |
| `FirmwareError` | `Exception` | Log and display firmware errors |

**Firmware-check setting:** In the checked-in `ConnectMedWand()` method, `const int n = 0`. The code comment defines `0` as **never check**, so this configuration does **not** periodically call `CheckFirmwareVersion()`. A firmware-update-required exception is still handled when raised. Review the firmware-check interval and firmware-update obligations in your MedWand agreement before deploying an application.

## Captured data in the sample

For ECG, camera, and stethoscope captures, the corresponding ViewModel calls `EcgBmpFromCapture(bytes)`, `CameraBmpFromCapture(bytes)`, or `StethoscopeWavFromCapture(bytes)`, respectively. The sample appends each returned value, along with a timestamp, to a relative `captures.txt` file. The README does not prescribe an output location for the underlying capture files beyond what the SDK methods implement.

## Source and SDK

- [`SampleWpfApp/SampleWpfApp.sln`](SampleWpfApp/SampleWpfApp.sln): Visual Studio solution.
- [`SampleWpfApp/SampleWpfApp.csproj`](SampleWpfApp/SampleWpfApp.csproj): .NET target and NuGet dependencies, including `MWSDK.Wpf`.
- [`SampleWpfApp/App.xaml.cs`](SampleWpfApp/App.xaml.cs): license and public-key fields.
- [`SampleWpfApp/MainWindow.xaml.cs`](SampleWpfApp/MainWindow.xaml.cs): `MedWandController` lifecycle, event dispatch, and `FirmwareController` usage.
- [`SampleWpfApp/Views/`](SampleWpfApp/Views): WPF sensor views and their ViewModels.

The sample imports SDK namespaces including `MWSDK.NetCore`, `MWSDK.NetCore.Core`, and `MWSDK.Wpf`. The public API references above reflect calls visible in this repository rather than an exhaustive contract or complete type declaration for every SDK member.

## Troubleshooting

- **License validation fails:** Confirm that `MwSdkLicense` and `MwSdkPublicKey` have been populated in `App.xaml.cs`. The sample rejects empty strings before calling `Construct(...)`.
- **MedWand not found:** Confirm the device connection and use the sample's retry prompt. `MainWindow` checks `IsConnected` after `Connect()`.
- **Initialization fails:** Review the initialization error displayed by `MainWindow`, along with `IsConnected`, `DeviceState`, and `IsInitialized`.
- **Firmware update is requested:** Keep the device connected and follow the application's confirmation and progress messages. The firmware update uses `FirmwareController.StartAsync()` and reports state, progress, and errors through its events.
- **Audio or camera behavior differs from the examples:** Inspect the relevant ViewModel and the selected device capabilities; the sample uses Windows audio endpoints and camera-specific properties.

For additional MedWand developer information, see the [MedWand developer support resources](https://support.medwand.com/portal/en/kb/medwand-support/developers).
