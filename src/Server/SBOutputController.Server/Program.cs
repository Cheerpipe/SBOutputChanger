using System;
using System.Windows.Forms;
using SBOutputController.Server.InstanceServices;
using SBOutputController.Shared;

namespace SBOutputController.Server
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            IInstanceService instanceService = new InstanceService();
            if (instanceService.IsAlreadyRunning())
            {
                return;
            }

            SbController sbOutputController = new SbController(@"C:\Program Files (x86)\Creative\Sound Blaster Command");
            SbControllerService.SetSbControllerInstance(sbOutputController);
            RpcServer server = new RpcServer();
            server.Start("SBOutputController");
            Application.Run();
        }
    }
}
