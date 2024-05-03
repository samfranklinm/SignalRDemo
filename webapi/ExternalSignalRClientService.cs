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

    public IAsyncEnumerable<string> RequestSentenceFromCustomGPT(CancellationToken ct)
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

        if (_connection.State == HubConnectionState.Connected && !ct.IsCancellationRequested)
        {
            IAsyncEnumerable<string> stream;
            try
            {
                stream = _connection.StreamAsync<string>("GetSentences", false, cancellationToken: ct);
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
                    yield break;
                }
                await Task.Delay(1000, ct);
                yield return sentence;
            }
            _logger.LogInformation("Streaming completed");
        }
        else if (ct.IsCancellationRequested)
        {
            Console.WriteLine("Cancellation requested, requesting ExternalStreamer to stop streaming...");

            IAsyncEnumerable<string> stream;
            try
            {
                stream = _connection.StreamAsync<string>("GetSentences", true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting stream: {ex.Message}");
                yield break;
            }

            await foreach (var sentence in stream)
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancellation requested, stopping stream...");
                    yield return sentence;
                }
                await Task.Delay(1000, ct);
                yield return sentence;
            }
        }
        else
        {
            _logger.LogError("Failed to connect and start streaming.");
            yield break;
        }
    }


}
