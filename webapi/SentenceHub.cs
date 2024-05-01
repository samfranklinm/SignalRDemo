using Microsoft.AspNetCore.SignalR;
using System.Threading;

namespace webapi.SentenceHub
{
    public class SentenceHub : Hub
    {
        private readonly ExternalSignalRClientService _externalClient;
        private CancellationTokenSource _streamCancellationTokenSource;

        public SentenceHub(ExternalSignalRClientService externalClient)
        {
            _externalClient = externalClient;
        }

        public async Task StreamToClientFromPlugin()
        {
            _streamCancellationTokenSource = new CancellationTokenSource();
            try
            {
                await foreach (var sentence in _externalClient.RequestSentenceFromExternalStreamer(_streamCancellationTokenSource.Token))
                {
                    if (_streamCancellationTokenSource.IsCancellationRequested)
                    {
                        _streamCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }
                    Clients.All.SendAsync("ReceiveFromPlugin", sentence);
                }
            }
            catch (OperationCanceledException)
            {
                Clients.Caller.SendAsync("StreamCancelled", "Streaming has been cancelled.");
            }
            finally
            {
                _streamCancellationTokenSource.Dispose();
            }
        }

        public async Task CancelStreaming()
        {
            if (_streamCancellationTokenSource != null && !_streamCancellationTokenSource.IsCancellationRequested)
            {
                _streamCancellationTokenSource.Cancel();
            }
        }

        public async Task StreamToClientFromWebApi()
        {
            var sentence = "Streaming this from my backyard (webapi - backend).";
            foreach (var word in sentence.Split(' '))
            {
                await Clients.All.SendAsync("ReceiveFromWebApi", word);
                await Task.Delay(100);
            }
        }
    }
}
