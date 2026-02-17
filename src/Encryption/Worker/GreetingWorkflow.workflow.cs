namespace TemporalioSamples.Encryption.Worker;

using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using Temporalio.Workflows;

[Workflow]
public class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        try
        {
            var result = await Workflow.ExecuteLocalActivityAsync(
               (GreetingWorkflow act) => act.RunActivityAsync("Gaurav"),
               new()
               {
                   StartToCloseTimeout = TimeSpan.FromSeconds(20),
                   RetryPolicy = new()
                   {
                       InitialInterval = TimeSpan.FromSeconds(1),
                       BackoffCoefficient = 10,
                       MaximumInterval = TimeSpan.FromSeconds(100),
                       MaximumAttempts = 20,
                   },
               });
            Workflow.Logger.LogInformation("Activity instance method result: {Result}", result);
        }
        catch (FailureException ex)
        {
            Workflow.Logger.LogError(ex, "Activity failed after retries");
            await Workflow.DelayAsync(TimeSpan.FromSeconds(30));
            throw new ApplicationFailureException("Failed", ex);
        }
        return $"Hello, {name}!";
    }

    [Activity]
    public async Task<string> RunActivityAsync(string name)
    {
        ActivityInfo info = ActivityExecutionContext.Current.Info;
        int attempt = info.Attempt; // 1, 2, 3, ...

        // await Task.Delay(30000);
        // if (attempt < 2)
        // {
        ActivityExecutionContext.Current.Logger.LogInformation(
            "Activity attempt {Attempt} failed, throwing to retry",
            attempt);
        throw new InvalidOperationException($"Simulated failure on attempt {attempt}");
        // }
        // return $"Hello from activity, {name}!";
    }
}