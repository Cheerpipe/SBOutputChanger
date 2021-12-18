using H.ProxyFactory;
using SBOutputController.Shared;

namespace TestClient
{
    internal class Program
    {
        static void Main()
        {
            var factory = new PipeProxyFactory();
            factory.InitializeAsync("SBOutputController");
            ISbControllerService service =  factory.CreateInstanceAsync<SbControllerService, ISbControllerService>().GetAwaiter().GetResult();
            service.SetHeadphones();
        }
    }
}
