using Temporalio.Workflows;

namespace TemporalioSamples.PatchingNondeterminism;

[Workflow("MyWorkflow")]
public class MyWorkflowV1Unpatched
{
    private bool proceed;
#pragma warning disable CS0649 // Assigned only when the Patched(...) line below is uncommented (simulates the Sep 9 deploy).
    private bool withdrawn;
#pragma warning restore CS0649

    [WorkflowRun]
    public async Task RunAsync()
    {
        // withdrawn = Workflow.Patched("broker-rejection-withdraw-email");
        await Workflow.WaitConditionAsync(() => proceed);

        Result = await Workflow.ExecuteActivityAsync(
            () => Activities.SendBrokerReviewEmail(),
            new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
        await Workflow.WaitConditionAsync(() => false);
    }

    [WorkflowSignal]
    public Task RandomAsync()
    {
        return Task.CompletedTask;
    }

    [WorkflowSignal]
    public Task ProceedAsync()
    {
        proceed = true;
        return Task.CompletedTask;
    }

    [WorkflowUpdate]
    public async Task<string> NudgeAsync()
    {
        if (withdrawn)
        {
            Result = "withdrawn";
        }
        else
        {
            Result = "nudged";
        }
        return Result;
    }

    [WorkflowQuery]
    public string? Result { get; private set; }
}
