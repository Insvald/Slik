using Slik.Security;
using System;
using System.Net.Http;

namespace Slik.Cache
{
    // https://sakno.github.io/dotNext/features/cluster/aspnetcore.html
    internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
    {
        private readonly ICommunicationCertifier _certifier;

        public RaftClientHandlerFactory(ICommunicationCertifier certifier)
        {
            _certifier = certifier;
        }

        public HttpMessageHandler CreateHandler(string name)
        {
            var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(100) };
            _certifier.SetupClient(handler.SslOptions);
            return handler;
        }
    }
}
