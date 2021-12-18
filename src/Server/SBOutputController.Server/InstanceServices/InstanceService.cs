using System;
using System.Threading;

namespace SBOutputController.Server.InstanceServices
{
    public class InstanceService : IInstanceService, IDisposable
    {
        public Mutex InstanceMutex { get; private set; }

        public bool IsAlreadyRunning()
        {
            InstanceMutex = new Mutex(true, "88fe6a05-77e1-45c9-93bd-cb8be0fc63da", out bool created);
            return !created;
        }

        public void Dispose()
        {
            InstanceMutex?.Dispose();
        }
    }
}
