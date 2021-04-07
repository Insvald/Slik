using System;
using System.Net.Http;
using System.Net.Security;

namespace Slik.Cache
{
    // https://sakno.github.io/dotNext/features/cluster/aspnetcore.html
    internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
    {
        private readonly Action<SslClientAuthenticationOptions>? _optionsSetter;

        public RaftClientHandlerFactory(Action<SslClientAuthenticationOptions>? optionsSetter = null)
        {
            _optionsSetter = optionsSetter;
        }

        public HttpMessageHandler CreateHandler(string name)
        {
            var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(100) };

            if (_optionsSetter != null)
            {
                _optionsSetter(handler.SslOptions);
            }
            else
            {
                handler.SslOptions.RemoteCertificateValidationCallback = (_, __, ___, ____) => true;
            }

            return handler;
        }
    }
}
