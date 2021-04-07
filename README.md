# SlikCache
Distributed In-process Cache in C# / Net 6.0 with external gRPC API (HTTP/2, client/server certificates)

Based on a [magnificent dotNext library](https://github.com/sakno/dotNext) and its Raft cluster implementation. 

* Implements [IDistributedCache](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache), a standard interface for .Net Core cache.

Simple initialization in ```Main()```:
```C#
await Host
    .CreateDefaultBuilder()
    .UseSlik(new SlikOptions 
    { 
        Host = new IPEndPoint(IPAddress.Loopback, 3092),
        Members = "localhost:3092,localhost:3093,localhost:3094"
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

## How to run a minimal cluster: 
```
SlikNode --port=3262 --folder="node 1" --members=localhost:3262,localhost:3263,localhost:3264
SlikNode --port=3263 --folder="node 2" --members=localhost:3262,localhost:3263,localhost:3264
SlikNode --port=3264 --folder="node 3" --members=localhost:3262,localhost:3263,localhost:3264
```
Sample project: [examples/SlikNode](https://github.com/Insvald/Slik/tree/master/examples/SlikNode).
