using System;
using BarRaider.SdTools;

namespace SBController.Plugin
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Uncomment this line of code to allow for debugging
            //while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }

            SDWrapper.Run(args);
        }
    }
}
