using Microsoft.AspNetCore.SignalR;

namespace webapi.SentenceHub
{
    public class SentenceHub : Hub
    {
        private readonly ExternalSignalRClientService _externalClient;
        private CancellationTokenSource _streamCancellationTokenSource = new CancellationTokenSource();
        private ManualResetEventSlim _stopRequested = new ManualResetEventSlim(false);

        public SentenceHub(ExternalSignalRClientService externalClient)
        {
            _externalClient = externalClient;
        }

        public async Task ManageStreaming(bool cancellationRequested)
        {
            DisposeResources();
            _streamCancellationTokenSource = new CancellationTokenSource();
            _stopRequested.Reset();

            var token = _streamCancellationTokenSource.Token;
            Task streamingTask = StreamToClientFromCustomGPT(token, cancellationRequested);
            //Task listeningTask = ListenForCancellation(token);


            Task runTasks = Task.WhenAny(streamingTask);
        }

        //private async Task ListenForCancellation(CancellationToken token)
        //{
        //    try
        //    {
        //        while (!_stopRequested.IsSet)
        //        {
        //            try
        //            {
        //                await Task.Delay(500, token); // This will throw if the token is cancelled
        //            }
        //            catch (OperationCanceledException)
        //            {
        //                // Token was cancelled, check if _stopRequested is set
        //                if (_stopRequested.IsSet)
        //                {
        //                    break;
        //                }
        //            }
        //        }
        //        _stopRequested.Set();  // Set stop requested when token is cancelled
        //    }
        //    finally
        //    {
        //        await Clients.All.SendAsync("StreamCancelled", "Streaming has been cancelled by user.");
        //    }
        //}


        private async Task StreamToClientFromCustomGPT(CancellationToken token, bool cancellationRequested)
        {
            try
            {
                while (!cancellationRequested)
                {
                    await foreach (var sentence in _externalClient.RequestSentenceFromCustomGPT(token).WithCancellation(token))
                    {
                        if (cancellationRequested)
                        {
                            break;
                        }
                        await Clients.All.SendAsync("ReceiveFromCustomGPT", sentence, token);
                    }
                }

                _stopRequested.Set();
                _streamCancellationTokenSource.Cancel();
                token = _streamCancellationTokenSource.Token;

                await foreach (var sen in _externalClient.RequestSentenceFromCustomGPT(token))
                {
                    Console.WriteLine(sen); 
                    await Clients.All.SendAsync("StreamCancelled", "Streaming has been manually stopped.");
                }
            }
            catch (OperationCanceledException)
            {
                await Clients.Caller.SendAsync("StreamCancelled", "Streaming has been cancelled from StreamToClientFromCustomGPT.");
            }
            finally
            {
                DisposeResources();
            }
        }



        public bool CancelStreaming()
        {
            if (_streamCancellationTokenSource != null)
            {
                _streamCancellationTokenSource.Cancel();
                _stopRequested.Set();
                Console.WriteLine("Manual and token cancellation requested.");
            }
            else
            {
                Console.WriteLine("No cancellation source available.");
            }
            return _stopRequested.IsSet;
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

        private void DisposeResources()
        {
            _streamCancellationTokenSource?.Dispose();
            _stopRequested?.Dispose();
            _stopRequested = new ManualResetEventSlim(false);  // Reinitialize for clean state
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
