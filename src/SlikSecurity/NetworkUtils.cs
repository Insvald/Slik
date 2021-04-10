using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Slik.Security
{
    public static class NetworkUtils
    {
        public static string GetLocalMachineName() => Dns.GetHostEntry(Dns.GetHostName()).HostName.ToLower();

        public static IEnumerable<IPAddress> GetLocalIPAddresses() => Dns
            .GetHostEntry(Dns.GetHostName())
            .AddressList
            .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork /*|| ip.AddressFamily == AddressFamily.InterNetworkV6*/);
    }
}
