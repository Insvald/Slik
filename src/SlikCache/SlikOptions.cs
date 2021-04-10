using DotNext.Net.Cluster.Consensus.Raft;
using Slik.Security;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;

namespace Slik.Cache
{
    public class SlikOptions
    {
        public const int DefaultPort = 3092;

        public IPEndPoint Host { get; set; } = new IPEndPoint(IPAddress.Loopback, DefaultPort);
        public IEnumerable<string> Members { get; set; } = Enumerable.Empty<string>();
        public string DataFolder { get; set; } = "";
        public bool EnableGrpcApi { get; set; }
        public int RecordsPerPartition { get; set; } = 50;
        public CertificateOptions CertificateOptions { get; set; } = new();        
        public PersistentState.Options PersistentStateOptions { get; set; } = new();

        public void CopyTo(SlikOptions options)
        {
            options.Host = Host;
            options.Members = Members;
            options.DataFolder = DataFolder;
            options.EnableGrpcApi = EnableGrpcApi;
            options.RecordsPerPartition = RecordsPerPartition;
            options.PersistentStateOptions = PersistentStateOptions;
            options.CertificateOptions = CertificateOptions;
        }
    }
}
