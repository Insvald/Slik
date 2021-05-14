using Grpc.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Slik.Cord
{
    public static class GrpcStreaming
    {
        public static async Task ClientAsync<Request, Response>(Request request,
            IServerStreamWriter<Response> responseStream,
            Func<Request, CallOptions, AsyncServerStreamingCall<Response>> streamingCallFunc,
            ServerCallContext context)
        {
            using var streamingCall = streamingCallFunc(request, context.ToCallOptions());
            await StreamTransferAsync(streamingCall.ResponseStream, responseStream, context.CancellationToken).ConfigureAwait(false);
        }

        public static async Task BiDirectionalAsync<Request, Response>(
            IAsyncStreamReader<Request> requestStream,
            IServerStreamWriter<Response> responseStream,
            Func<CallOptions, AsyncDuplexStreamingCall<Request, Response>> streamingCallFunc,
            ServerCallContext context)
        {
            try
            {
                using var streamingCall = streamingCallFunc(context.ToCallOptions());

                var requestTransfer = StreamTransferAsync(requestStream, streamingCall.RequestStream, context.CancellationToken)
                    .ContinueWith(_ => streamingCall.RequestStream.CompleteAsync(), context.CancellationToken);

                var responseTransfer = StreamTransferAsync(streamingCall.ResponseStream, responseStream, context.CancellationToken);

                await Task.WhenAll(requestTransfer, responseTransfer).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // just quitting
            }
        }

        private static async Task StreamTransferAsync<T>(IAsyncStreamReader<T> inputStream, IAsyncStreamWriter<T> outputStream, CancellationToken token)
        {
            try
            {
                await foreach (var contentResponse in inputStream.ReadAllAsync(token).ConfigureAwait(false))
                {
                    await outputStream.WriteAsync(contentResponse).ConfigureAwait(false);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                // stream cancelled
            }            
        }
    }
}
