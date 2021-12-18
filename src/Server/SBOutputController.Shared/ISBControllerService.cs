using System;

namespace SBOutputController.Shared
{
    public interface ISbControllerService
    {
        void SetSpeakers();
        void SetHeadphones();
        void EnableDirect();
        void DisableDirect();
        DeviceOutputModes GetCurrentOutputMode();

        event EventHandler<OutputModeChangedEventArgs> OutputChanged;
    }
}
