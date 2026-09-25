using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Common.EnvConfig;
using Temporalio.Worker;
using TemporalioSamples.PatchingNondeterminism;

var connectOptions = ClientEnvConfig.LoadClientConnectOptions();
connectOptions.TargetHost ??= "localhost:7233";
connectOptions.LoggerFactory = LoggerFactory.Create(builder =>
    builder.
        AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ").
        SetMinimumLevel(LogLevel.Information));
var client = await TemporalClient.ConnectAsync(connectOptions);

const string TaskQueue = "patching-nondeterminism-task-queue";

async Task RunWorkerAsync()
{
    using var tokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        tokenSource.Cancel();
        eventArgs.Cancel = true;
    };

    var workerOptions = new TemporalWorkerOptions(taskQueue: TaskQueue)
        .AddActivity(Activities.SendBrokerReviewEmail)
        .AddActivity(Activities.WithdrawBrokerRejectionEmail)
        .AddWorkflow<MyWorkflowV1Unpatched>();

    Console.WriteLine("Running worker");
    using var worker = new TemporalWorker(client, workerOptions);

    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Worker cancelled");
    }
}

async Task RunStarterAsync()
{
    var workflowId = args.ElementAtOrDefault(2);
    if (workflowId is null)
    {
        throw new ArgumentException("Workflow id is required");
    }

    switch (args.ElementAtOrDefault(1))
    {
        case "--start-workflow":
            {
                var handle = await client.StartWorkflowAsync(
                    (MyWorkflowV1Unpatched wf) => wf.RunAsync(),
                    new(id: workflowId, taskQueue: TaskQueue));
                Console.WriteLine($"Started workflow with ID {handle.Id} and run ID {handle.ResultRunId}");
                break;
            }

        case "--proceed-workflow":
            {
                var handle = client.GetWorkflowHandle<MyWorkflowV1Unpatched>(workflowId);
                await handle.SignalAsync(wf => wf.ProceedAsync());
                Console.WriteLine($"Sent ProceedAsync signal to workflow with ID {handle.Id}");
                break;
            }

        case "--update-workflow":
            {
                var handle = client.GetWorkflowHandle<MyWorkflowV1Unpatched>(workflowId);
                var result = await handle.ExecuteUpdateAsync(wf => wf.NudgeAsync());
                Console.WriteLine($"Update result for ID {handle.Id}: {result}");
                break;
            }

        case "--query-workflow":
            {
                var handle = client.GetWorkflowHandle(workflowId);
                var result = await handle.QueryAsync((MyWorkflowV1Unpatched wf) => wf.Result);
                Console.WriteLine($"Query result for ID {handle.Id}: {result}");
                break;
            }

        default:
            throw new ArgumentException("Must pass one of --start-workflow, --proceed-workflow, --update-workflow, or --query-workflow");
    }
}

switch (args.ElementAtOrDefault(0))
{
    case "worker":
        await RunWorkerAsync();
        break;
    case "starter":
        await RunStarterAsync();
        break;
    default:
        throw new ArgumentException("Must pass 'worker' or 'starter' as the first argument");
}
