using Grpc.Core;

namespace Slik.Cord
{
    public static class ServerCallContextExtensions
    {
        public const string NamespaceKey = "containerd-namespace";

        public static CallOptions ToCallOptions(this ServerCallContext context)
        {
            // adding default namespace if the namespace is not specified,
            // because containerd requires some namespace
            var namespaceEntry = context.RequestHeaders.Get(NamespaceKey);
            if (string.IsNullOrEmpty(namespaceEntry?.Value))
            {
                context.RequestHeaders.Add(NamespaceKey, "default");
            }

            return new(context.RequestHeaders, context.Deadline, context.CancellationToken, context.WriteOptions);
        }
    }
}
