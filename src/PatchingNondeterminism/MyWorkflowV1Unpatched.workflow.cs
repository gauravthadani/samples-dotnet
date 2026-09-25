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
        // if (Workflow.Info.StartTime < new DateTime(2026, 9, 24, 15, 41, 24, DateTimeKind.Utc))
        // {
        // Console.WriteLine("before patched");
        // withdrawn = Workflow.Patched("broker-rejection-withdraw-email");
        Workflow.DeprecatePatch("broker-rejection-withdraw-email");

        // withdrawn = Workflow.Patched("broker-rejection-withdraw-email_v2");

        // // 24 Sept 2026, 23:24:47.76 UTC
        // if (Workflow.Info.StartTime < new DateTime(2026, 9, 24, 23, 24, 48, DateTimeKind.Utc))
        // {
        //     Console.WriteLine("inside if - by wf info start time");
        //     Workflow.DeprecatePatch("broker-rejection-withdraw-email");
        // }
        // Console.WriteLine("after patched");
        // }
        // Workflow.DeprecatePatch("broker-rejection-withdraw-email");
        // Workflow.DeprecatePatch("broker-rejection-withdraw-email1234");
        Console.WriteLine("awaiting");

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
        withdrawn = Workflow.Patched("broker-rejection-withdraw-email_v1");
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
