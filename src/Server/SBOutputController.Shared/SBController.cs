using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SBOutputController.Shared
{
    public class DeviceWrapper
    {
        public DeviceWrapper(dynamic device)
        {
            NativeDevice = device;
            DeviceName = device.DeviceName;
        }

        public string DeviceName { get; }
        public dynamic NativeDevice { get; }
    }

    class NoDevicesFound : Exception
    {
        public NoDevicesFound(string message)
            : base(message)
        { }
    }

    class MethodNotFound : Exception
    {
        public MethodNotFound(string message)
            : base(message)
        { }
    }

    public enum DeviceOutputModes : uint
    {
        Speakers = 2U,
        Headphones = 4U
    }

    public enum DirectModeStates : uint
    {
        Off = 0U,
        On = 1U
    }

    public class OutputModeChangedEventArgs : EventArgs
    {
        public OutputModeChangedEventArgs(DeviceOutputModes outputMode)
        {
            OutputMode = outputMode;
        }

        public DeviceOutputModes OutputMode { get; }
    }

    public enum DllLoadedStatus
    {
        Missing,
        Registered
    }

    public readonly struct SbRequiredDll
    {
        public SbRequiredDll(string clsId, string relativePath, string fullPath, DllLoadedStatus dllStatus)
        {
            ClsId = clsId;
            RelativePath = relativePath;
            FullPath = fullPath;
            Status = dllStatus;
        }

        public string ClsId { get; }
        public string RelativePath { get; }
        public string FullPath { get; }
        public DllLoadedStatus Status { get; }
    }

    public class SbController
    {
        public static readonly Dictionary<string, string> RequiredDllsList = new Dictionary<string, string>()
        {
            { "{495E4C24-85ED-4f19-885E-C2D01D7EA26C}", @"Platform\SndCrUSB.dll" }
        };

        public static string SbConnectExecutable = @"Creative.SBCommand.exe";
        public static string SbConnectPath = @"C:\Program Files (x86)\Creative\Sound Blaster Command\";

        public event EventHandler<OutputModeChangedEventArgs> OutputModeChangedEvent;

        public SbController(string sbDirectory)
        {
            _sbDirectory = sbDirectory;
            _activeDevice = null;

            Initialize();
        }
        [STAThread]
        private void Initialize()
        {
            string log4NetDllPath = Path.Combine(_sbDirectory, "Package", "log4net.dll");
            string devicesDllPath = Path.Combine(_sbDirectory, "Platform", "Creative.Platform.Devices.dll");

            Assembly.LoadFrom(log4NetDllPath);
            _sbDevicesDll = Assembly.LoadFrom(devicesDllPath);
            Type deviceManagerType = _sbDevicesDll.GetType("Creative.Platform.Devices.Models.DeviceManager", true);
            Type deviceEndpointSelectionServiceType = _sbDevicesDll.GetType("Creative.Platform.Devices.Selections.DeviceEndpointSelectionService", true);

            MethodInfo deviceManagerInstanceMethod = deviceManagerType.GetMethod("get_Instance");
            if (deviceManagerInstanceMethod == null)
            {
                throw new MethodNotFound("Failed to find DeviceManager.Instance");
            }
            _deviceManager = deviceManagerInstanceMethod.Invoke(null, null);
            //_deviceManager.Initialize();

            if (_deviceManager.DiscoveredDevices.Count == 0)
            {
                throw new NoDevicesFound("No devices found");
            }

            MethodInfo endpointServiceInstanceMethod = deviceEndpointSelectionServiceType.GetMethod("get_Instance");
            if (endpointServiceInstanceMethod == null)
            {
                throw new MethodNotFound("Failed to find DeviceEndpointSelectionService.Instance");
            }
            _deviceEndpointSelectionService = endpointServiceInstanceMethod.Invoke(null, null);
        }

        public static bool VerifySetup(string exePath)
        {
            if (!VerifyExecutablePath(exePath))
                return false;

            string sbDirectory = Path.GetDirectoryName(exePath);
            List<SbRequiredDll> requiredDllStatus = GetRequiredDlLs(sbDirectory);

            return requiredDllStatus.All(dll => dll.Status == DllLoadedStatus.Registered);
        }

        public static bool VerifyExecutablePath(string exePath)
        {
            return exePath != null && exePath.EndsWith(SbConnectExecutable) && File.Exists(exePath);
        }

        public static List<SbRequiredDll> GetRequiredDlLs(string sbDirectory)
        {
            var result = new List<SbRequiredDll>();

            foreach (var item in RequiredDllsList)
            {
                using (var classesRootKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.ClassesRoot, Microsoft.Win32.RegistryView.Default))
                {
                    string clsId = item.Key;

                    var clsIdKey =
                        classesRootKey.OpenSubKey(@"Wow6432Node\CLSID\" + clsId) ??
                        classesRootKey.OpenSubKey(@"CLSID\" + clsId);

                    string expectedDllFullPath = Path.Combine(sbDirectory, item.Value);

                    if (clsIdKey == null)
                    {
                        result.Add(new SbRequiredDll(clsId, item.Value, expectedDllFullPath, DllLoadedStatus.Missing));
                        continue;
                    }

                    var inprocKey = clsIdKey.OpenSubKey("InprocServer32");
                    clsIdKey.Dispose();
                    if (inprocKey == null)
                    {
                        result.Add(new SbRequiredDll(clsId, item.Value, expectedDllFullPath, DllLoadedStatus.Missing));
                        continue;
                    }

                    string regDllFullPath = inprocKey.GetValue(null).ToString();
                    inprocKey.Dispose();

                    result.Add(
                        String.Compare(regDllFullPath, expectedDllFullPath, StringComparison.OrdinalIgnoreCase) == 0
                            ? new SbRequiredDll(clsId, item.Value, expectedDllFullPath, DllLoadedStatus.Registered)
                            : new SbRequiredDll(clsId, item.Value, expectedDllFullPath, DllLoadedStatus.Missing));
                }
            }

            return result;
        }

        public void SetActiveDevice(DeviceWrapper deviceWrapper)
        {
            if (_activeDevice == null || _activeDevice.NativeDevice != deviceWrapper.NativeDevice)
            {
                _activeDevice = deviceWrapper;
                _outputModeFeature = _deviceEndpointSelectionService.GetAggregatedFeature(deviceWrapper.NativeDevice, "MultiplexOutputFeatureId");
                _stereoDirectFeature = _deviceEndpointSelectionService.GetAggregatedFeature(deviceWrapper.NativeDevice, "StereoDirectFeatureId");

                Type valueChangedDelegateType = _sbDevicesDll.GetType("Creative.Platform.Devices.Selections.EffectParameterValueChangedDelegate");
                dynamic valueChangedDelegate = Delegate.CreateDelegate(valueChangedDelegateType, this, "OnOutputModeChanged");
                // EffectParameterValueChangedHanlder is the "correct" spelling.... Thanks Creative
                _outputModeFeature.EffectParameterValueChangedHanlder += valueChangedDelegate;
            }
        }

        public List<DeviceWrapper> GetDevices()
        {
            List<DeviceWrapper> devices = new List<DeviceWrapper>();

            foreach (var device in _deviceManager.DiscoveredDevices)
            {
                devices.Add(new DeviceWrapper(device));
            }

            return devices;
        }

        public void OnOutputModeChanged(object parameters)
        {
            if (_activeDevice != null)
            {
                DeviceOutputModes outputMode = GetOutputModeForDevice(_activeDevice);
                OutputModeChangedEvent?.Invoke(this, new OutputModeChangedEventArgs(outputMode));
            }
        }

        public DeviceOutputModes GetOutputModeForDevice(DeviceWrapper deviceWrapper)
        {
            SetActiveDevice(deviceWrapper);

            uint outputModeValue = _outputModeFeature.GetValue<uint>("MultiplexOutputParameterId");
            return (DeviceOutputModes)outputModeValue;
        }

        public void SwitchToOutputMode(DeviceWrapper deviceWrapper, DeviceOutputModes requestedOutputModeEnum)
        {
            SetActiveDevice(deviceWrapper);

            uint requestedOutputMode = (uint)requestedOutputModeEnum;
            uint outputModeValue = _outputModeFeature.GetValue<uint>("MultiplexOutputParameterId");
            if (outputModeValue != requestedOutputMode)
            {
                _outputModeFeature.SetValue<uint>(requestedOutputMode, "MultiplexOutputParameterId");
            }
        }

        public void SwitchDirectMode(DeviceWrapper deviceWrapper, DirectModeStates requestedDirectModeEnum)
        {
            SetActiveDevice(deviceWrapper);

            uint requestedDirectMode = (uint)requestedDirectModeEnum;
            uint directModeValue = _stereoDirectFeature.GetValue<uint>("StereoDirectParameterId");
            if (directModeValue != requestedDirectMode)
            {
                _stereoDirectFeature.SetValue<uint>(requestedDirectMode, "StereoDirectParameterId");
            }
        }

        private Assembly _sbDevicesDll;

        private dynamic _deviceManager;
        private dynamic _deviceEndpointSelectionService;
        private dynamic _outputModeFeature;
        private dynamic _stereoDirectFeature;

        private readonly string _sbDirectory;

        private DeviceWrapper _activeDevice;
    }
}
    