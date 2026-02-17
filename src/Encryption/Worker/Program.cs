using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Worker;
using TemporalioSamples.Encryption.Codec;
using TemporalioSamples.Encryption.Worker;

// Create a client to localhost on default namespace
var dataConverter = DataConverter.Default with { PayloadCodec = new EncryptionCodec() };
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    DataConverter = dataConverter,
    Interceptors = new[] { new ContextPropagationInterceptor<string?>(MyContext.UserIdLocal, DataConverter.Default.PayloadConverter), },
    LoggerFactory = LoggerFactory.Create(builder =>
        builder.
            AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
            SetMinimumLevel(LogLevel.Information)),
});

// Cancellation token cancelled on ctrl+c
using var tokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    tokenSource.Cancel();
    eventArgs.Cancel = true;
};

// Run worker until cancelled
Console.WriteLine("Running worker");
var activity = new GreetingWorkflow();
using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions(taskQueue: "encryption-sample").
        AddActivity(activity.RunActivityAsync).
        AddWorkflow<GreetingWorkflow>());
try
{
    await worker.ExecuteAsync(tokenSource.Token);
    // await worker.ExecuteAsync();
}
catch (OperationCanceledException)
{
    Console.WriteLine("Worker cancelled");
}