using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using StreamJsonRpc;

namespace PbixTools
{
    /// <summary>
    /// ... An instance has the same lifetime as the surrounding process.
    /// Multiple sessions can be launched inside a <see cref="RpcServer"/> instance.
    /// </summary>
    public class RpcServer : IDisposable
    {
        private readonly static ILogger Log = Serilog.Log.ForContext<RpcServer>();

        public static RpcServer Start(Func<Stream> sender, Func<Stream> receiver, CancellationTokenSource cts)
        {
            var server = new RpcServer(sender(), receiver(), e => 
            {
                // e.Reason == DisconnectedReason.
                // Disposed, ParseError, StreamError, Unknown
                Log.Information($"Disconnect Reason: {e.Reason} - {e.Description}");
                cts.Cancel();
            });

            server.AnnounceServerAsync().Wait();

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
            _onDisconnected(new JsonRpcDisconnectedEventArgs("Client requested server shutdown", DisconnectedReason.Unknown));
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