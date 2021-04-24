[![Build](https://github.com/Insvald/Slik/actions/workflows/build-and-tests.yml/badge.svg)](https://github.com/Insvald/Slik/actions/workflows/build-and-tests.yml)
[![Nuget](https://img.shields.io/nuget/v/Slik.Cache)](https://www.nuget.org/api/v2/package/Slik.Cache/1.0.0)
[![The current version of Slik.Cache](https://img.shields.io/github/v/release/Insvald/Slik)](https://github.com/Insvald/Slik)
[![Slik.Cache uses MIT License](https://img.shields.io/github/license/Insvald/Slik)](https://github.com/Insvald/Slik/blob/master/LICENSE)

# Slik.Cache
Distributed In-process Cache in C# / Net 6.0 with external gRPC API (HTTP/2, client/server certificates)

Based on a [magnificent dotNext library](https://github.com/sakno/dotNext) and its Raft cluster implementation. 

Implements [IDistributedCache](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache), a standard interface for .Net Core cache.

Simple initialization:
```C#
await Host
    .CreateDefaultBuilder()
    .UseSlik(new SlikOptions 
    { 
        Host = new IPEndPoint(IPAddress.Loopback, 3092),
        Members = new[] { "localhost:3092", "localhost:3093", "localhost:3094" }
    })
    .Build()
    .RunAsync();
```

Usage:
```C#
public class CacheConsumer
{
  private readonly IDistributedCache _cache;

  public CacheConsumer(IDistributedCache cache)
  {
      _cache = cache;
      _cache.SetString("Greeting", "Hello, world");
  }
  
  //...  
}
```
Update any node, updates are redirected to a cluster leader, and are replicated automatically to each node.

## Sample project: [examples/SlikNode](https://github.com/Insvald/Slik/tree/master/examples/SlikNode)

How to run a minimal cluster: 
```
SlikNode --port=3092 --folder="node 1" --members=localhost:3092,localhost:3093,localhost:3094
SlikNode --port=3093 --folder="node 2" --members=localhost:3092,localhost:3093,localhost:3094
SlikNode --port=3094 --folder="node 3" --members=localhost:3092,localhost:3093,localhost:3094
```

## Roadmap
- [x] Self-signed certificates generation
- [x] Support adding/removal of cluster members in runtime
- [x] More unit and integration tests to cover adding/removing cluster members
- [ ] Docker compose for starting cluster in containers
