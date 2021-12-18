using System;
using System.Linq;
using System.Windows.Forms;

namespace SBOutputController.Shared
{
    public class SbControllerService : ISbControllerService
    {
        private static SbController _sbController;
        private static Form _dispatcherForm = new Form();
        public static void SetSbControllerInstance(SbController controller)
        {
            _sbController = controller;

            //Force handle creation
            _ = _dispatcherForm.Handle;
        }

        public SbControllerService()
        {
            _sbController.OutputModeChangedEvent += _sbController_OutputModeChangedEvent;
        }

        private void _sbController_OutputModeChangedEvent(object sender, OutputModeChangedEventArgs e)
        {
            OutputChanged?.Invoke(this, e);
        }

        public void SetSpeakers()
        {
            _dispatcherForm.Invoke((MethodInvoker)(() =>
            {
                _sbController.SwitchToOutputMode(_sbController.GetDevices().FirstOrDefault(), DeviceOutputModes.Speakers);
            }));
        }

        public void SetHeadphones()
        {
            _dispatcherForm.Invoke((MethodInvoker)(() =>
            {
                _sbController.SwitchToOutputMode(_sbController.GetDevices().FirstOrDefault(), DeviceOutputModes.Headphones);
            }));
        }

        public void EnableDirect()
        {
            _dispatcherForm.Invoke((MethodInvoker)(() =>
            {
                _sbController.SwitchDirectMode(_sbController.GetDevices().FirstOrDefault(), DirectModeStates.On);
            }));
        }

        public void DisableDirect()
        {
            _dispatcherForm.Invoke((MethodInvoker)(() =>
            {
                _sbController.SwitchDirectMode(_sbController.GetDevices().FirstOrDefault(), DirectModeStates.Off);
            }));
        }

        public DeviceOutputModes GetCurrentOutputMode()
        {
            DeviceOutputModes currentMode = default(DeviceOutputModes);
            _dispatcherForm.Invoke((MethodInvoker)(() =>
            {
                currentMode = _sbController.GetOutputModeForDevice(_sbController.GetDevices().FirstOrDefault());
            }));
            return currentMode;
        }

        public event EventHandler<OutputModeChangedEventArgs> OutputChanged;
    }
}
