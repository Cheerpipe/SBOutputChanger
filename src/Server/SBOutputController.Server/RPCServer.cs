using H.ProxyFactory;

namespace SBOutputController.Server
{
    public class RpcServer
    {
        readonly PipeProxyServer _server = new PipeProxyServer();
        public void Start(string pipeName)
        {
            _server.InitializeAsync(pipeName);
        }
    }
}
