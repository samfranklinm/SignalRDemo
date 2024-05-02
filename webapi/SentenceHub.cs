using Microsoft.AspNetCore.SignalR;

namespace webapi.SentenceHub
{
    public class SentenceHub : Hub
    {
        private readonly ExternalSignalRClientService _externalClient;
        private CancellationTokenSource _streamCancellationTokenSource;

        public SentenceHub(ExternalSignalRClientService externalClient)
        {
            _externalClient = externalClient;
            _streamCancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StreamToClientFromCustomGPT()
        {
            var token = _streamCancellationTokenSource.Token;
            try
            {
                await foreach (var sentence in _externalClient.RequestSentenceFromCustomGPT(token))
                {
                    await Clients.All.SendAsync("ReceiveFromPlugin", sentence, token);
                }
                if (token.IsCancellationRequested)
                    await Clients.All.SendAsync("StreamCancelled", "Streaming has been cancelled.");
            }
            catch (OperationCanceledException)
            {
                await Clients.Caller.SendAsync("StreamCancelled", "Streaming has been cancelled.");
            }
            finally
            {
                _streamCancellationTokenSource.Dispose();
            }
        }

        public void CancelStreaming()
        {
            _streamCancellationTokenSource.Cancel();
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
