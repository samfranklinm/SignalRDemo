using Microsoft.AspNetCore.SignalR.Client;
using System.Runtime.CompilerServices;

public class ExternalSignalRClientService
{
    private readonly HubConnection _connection;
    private readonly ILogger<ExternalSignalRClientService> _logger;
    private TaskCompletionSource<string> _tcs;

    public ExternalSignalRClientService(ILogger<ExternalSignalRClientService> logger)
    {
        _logger = logger;
        _connection = new HubConnectionBuilder()
            .WithUrl("https://localhost:7109/sentenceHub")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string>("ReceiveSentence", sentence =>
        {
            _logger.LogInformation("Received sentence: {Message}", sentence);
            _tcs.TrySetResult(sentence);
        });

        InitializeConnectionAsync().Wait();
    }

    private async Task InitializeConnectionAsync()
    {
        try
        {
            await _connection.StartAsync();
            _logger.LogInformation("SignalR client connection started.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Could not connect to the server: {ex.Message}");
        }
    }

    public IAsyncEnumerable<string> RequestSentenceFromExternalStreamer(CancellationToken ct)
    {
        return StreamSentencesAsync(ct);
    }

    private async IAsyncEnumerable<string> StreamSentencesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        if (_connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Connection is not active, attempting to reconnect...");
            try
            {
                await _connection.StartAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not connect to the server: {ex.Message}");
                yield break;
            }
        }

        if (_connection.State == HubConnectionState.Connected)
        {
            IAsyncEnumerable<string> stream;
            try
            {
                stream = _connection.StreamAsync<string>("GetSentence", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting stream: {ex.Message}");
                yield break;
            }

            await foreach (var sentence in stream.WithCancellation(ct).ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancellation requested, stopping stream...");
                    break;
                }
                await Task.Delay(1000, ct);
                yield return sentence;
            }
            _logger.LogInformation("Streaming completed");
        }
        else
        {
            _logger.LogError("Failed to connect and start streaming.");
            yield break;
        }
    }


}
