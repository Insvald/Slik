using DotNext.Net.Cluster.Consensus.Raft;
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

        internal PersistentState.Options PersistentStateOptions = new();

        public int BufferSize 
        {
            get => PersistentStateOptions.BufferSize;
            set => PersistentStateOptions.BufferSize = value;
        }

        public long InitialPartitionSize 
        { 
            get => PersistentStateOptions.InitialPartitionSize; 
            set => PersistentStateOptions.InitialPartitionSize = value; 
        }

        public bool UseCaching 
        { 
            get => PersistentStateOptions.UseCaching;  
            set => PersistentStateOptions.UseCaching = value; 
        }

        public int MaxConcurrentReads 
        { 
            get => PersistentStateOptions.MaxConcurrentReads; 
            set => PersistentStateOptions.MaxConcurrentReads = value; 
        }

        public bool ReplayOnInitialize 
        { 
            get => PersistentStateOptions.ReplayOnInitialize; 
            set => PersistentStateOptions.ReplayOnInitialize = value; 
        }

        public CompressionLevel BackupCompression 
        { 
            get => PersistentStateOptions.BackupCompression;
            set => PersistentStateOptions.BackupCompression = value; 
        }

        public void CopyTo(SlikOptions options)
        {
            options.Host = Host;
            options.Members = Members;
            options.DataFolder = DataFolder;
            options.EnableGrpcApi = EnableGrpcApi;
            options.RecordsPerPartition = RecordsPerPartition;
            options.PersistentStateOptions = PersistentStateOptions;
        }
    }
}
