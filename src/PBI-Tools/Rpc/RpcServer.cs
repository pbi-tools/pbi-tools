using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using StreamJsonRpc;

namespace PbiTools.Rpc
{
    /// <summary>
    /// ... An instance has the same lifetime as the surrounding process.
    /// Multiple sessions can be launched from inside a <see cref="RpcServer"/> instance.
    /// </summary>
    public class RpcServer : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<RpcServer>();

        public static RpcServer Start(Func<Stream> sender, Func<Stream> receiver, CancellationTokenSource cts)
        {
            var server = new RpcServer(sender(), receiver(), e => 
            {
                // e.Reason == DisconnectedReason.
                // Disposed, ParseError, StreamError, Unknown
                Log.Information($"Disconnect Reason: {e.Reason} - {e.Description}");
                cts.Cancel();
            });

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            server.AnnounceServerAsync().Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits

            return server;
        }


        private readonly JsonRpc _jsonRpc;
        private readonly Stream _sendingStream;
        private readonly Stream _receivingStream;
        private readonly Action<JsonRpcDisconnectedEventArgs> _onDisconnected;

        public RpcServer(Stream sendingStream, Stream receivingStream, Action<JsonRpcDisconnectedEventArgs> onDisconnected)
        {
            this._sendingStream = sendingStream;
            this._receivingStream = receivingStream;
            this._onDisconnected = onDisconnected;

            this._jsonRpc = JsonRpc.Attach(sendingStream, receivingStream, this);
            _jsonRpc.Disconnected += (sender,e) =>
            {
                _onDisconnected(e);
            };
        }

        internal async Task AnnounceServerAsync()  // non-public server methods cannot be called
        {
            await _jsonRpc.NotifyAsync("message/information", "Server started successfully");
        }

        // async methods: suffix can be omitted

#region Server Methods

        // +++ other methods +++
        // - getInfo
        // - startSession (open file/folder)
        // - endSession
        // * Session methods
        //   - list files

        [JsonRpcMethod("shutdown")]
        public void Shutdown()
        {
            // TODO Need to explicitly stop server here? This feels dodgy...
            _onDisconnected(new JsonRpcDisconnectedEventArgs("Client requested server shutdown", DisconnectedReason.RemotePartyTerminated));
            // ??? Do it this way: ??
            (this as IDisposable).Dispose();
        }

#endregion

        void IDisposable.Dispose()
        {
            _jsonRpc.Dispose();
            _sendingStream.Dispose();
            _receivingStream.Dispose();
        }
    }
}