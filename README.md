[![Build](https://github.com/Insvald/Slik/actions/workflows/build-and-tests.yml/badge.svg)](https://github.com/Insvald/Slik/actions/workflows/build-and-tests.yml)
[![Integration tests](https://github.com/Insvald/Slik/actions/workflows/integration-tests.yml/badge.svg)](https://github.com/Insvald/Slik/actions/workflows/integration-tests.yml)
[![Nuget](https://img.shields.io/nuget/v/Slik.Cache)](https://www.nuget.org/api/v2/package/Slik.Cache/1.0.0)
[![The current version of Slik.Cache](https://img.shields.io/github/v/release/Insvald/Slik)](https://github.com/Insvald/Slik)
[![Slik.Cache uses MIT License](https://img.shields.io/github/license/Insvald/Slik)](https://github.com/Insvald/Slik/blob/master/LICENSE)

# Slik.Cache
Distributed In-process Cache in C# and Net 5.0/6.0 with external gRPC API (HTTP/2, client/server certificates)

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

[![Slik.Cord tests](https://github.com/Insvald/Slik/actions/workflows/slik-cord-integration.yml/badge.svg)](https://github.com/Insvald/Slik/actions/workflows/slik-cord-integration.yml)
# Slik.Cord

A gRPC HTTP proxy for [containerd](https://github.com/containerd/containerd) in C# and Net 5.0/6.0. 
Containerd API works locally via Unix domain socket (in Linux) or named pipe (in Windows), not allowing to connect to it from another computer/container. This proxy can solve the problem.
**Current implementation doesn't work on Windows.**

## Usage
Run SlikCord (preferably in a container). Connect to port 80 from any client with gRPC support using the regular [containerd API](https://github.com/containerd/containerd/tree/master/api).

## Roadmap
- [x] Containers, images, version APIs supported
- [ ] Support more APIs
- [ ] Switch to HTTPS
- [ ] Support self-signed certificates
- [ ] Named pipes support
- [x] Unix domain socket on Linux support
- [ ] Unix domain socket on Windows support
