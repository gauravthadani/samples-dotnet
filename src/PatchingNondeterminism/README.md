# PatchingNondeterminism Sample

Reproduces the `[TMPRL1100] Non-deprecated patch marker encountered ... but there is no corresponding change command` nondeterminism error that occurs when `Workflow.Patched` is introduced without preserving the pre-patch branch for workflows that are already in flight.

This is the **anti-pattern**. For the correct staged rollout (`Patched` with `else` → `DeprecatePatch` → remove), see the sibling [Patching](../Patching) sample.

## Scenario

A `BrokerReview` workflow was originally deployed without any patch. It starts, then blocks on a signal (`ProceedAsync`) waiting for a downstream event. Weeks later, a new build ships that wraps the post-signal work in `Workflow.Patched("broker-rejection-withdraw-email")` and **drops the pre-patch branch**. In-flight runs — the ones that had already started under the old build and were still parked on the signal — cannot replay under the new build. They fail workflow tasks continuously with the NDE.

To run, first see [README.md](../../README.md) for prerequisites.

## Reproducing the error

### 1. Start the pre-patch worker (the "old build")

    dotnet run worker --workflow v1

### 2. Start a workflow — it will block on `WaitConditionAsync`

In a second terminal:

    dotnet run starter --start-workflow bad-patch-run-1

In the Temporal Web UI the run's history stops after the first `WorkflowTaskCompleted`. This is the "in-flight" run.

### 3. Stop the V1 worker (Ctrl+C) and start the V2 worker (the "new build" with the bad patch)

    dotnet run worker --workflow v2

### 4. Signal the in-flight workflow

    dotnet run starter --signal-workflow bad-patch-run-1

The V2 worker picks up the activation and fails the workflow task with:

    [TMPRL1100] Nondeterminism error: Non-deprecated patch marker encountered for change broker-rejection-withdraw-email, but there is no corresponding change command!

The workflow enters a task-failure retry loop — exactly the state customers see in production.

### 5. Confirm that a *fresh* V2 run works fine

    dotnet run starter --start-workflow fresh-run-1
    dotnet run starter --signal-workflow fresh-run-1

Fresh runs succeed because the patch marker gets written into history from the very first workflow task, so all subsequent replays line up. The NDE only affects runs that started under V1.

## Why this happens

`Workflow.Patched("id")` records a patch marker command on any non-replay activation where it returns `true`. Patch markers are workflow-scoped: once a marker for a given id is anywhere in history, every `Patched(id)` call for that run (including during replay of earlier events) must return `true`.

For an in-flight run started under V1:

1. V1 history has **no** marker for `broker-rejection-withdraw-email`.
2. V2 worker gets an activation. It replays events 1..N; `Patched()` isn't reached during replay because V1's execution path stopped inside `WaitConditionAsync` before that line.
3. The signal delivers, the workflow proceeds past the `await`, and `Workflow.Patched(...)` runs for the first time on this non-replay activation. The SDK writes the patch marker at the current event position and `Patched()` returns `true`. The workflow runs the new branch.
4. On the **next** replay of the same run (any subsequent activation), the SDK sees the marker in history and expects a matching `Patched()` command sequence starting from earlier in the run. The old history events don't provide one → NDE.

The trigger isn't `Patched()` itself — it's that V2 has **no `else` branch**. Every run of V2, replayed or not, must be able to reconstruct a history-compatible sequence. Without the pre-patch branch, runs started under V1 have no compatible path.

## Fixing it

Keep the pre-patch branch until every workflow started under V1 has completed. The corrected V2:

```csharp
if (Workflow.Patched("broker-rejection-withdraw-email"))
{
    await Workflow.ExecuteActivityAsync(
        () => Activities.WithdrawBrokerRejectionEmail(),
        new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
}
else
{
    await Workflow.ExecuteActivityAsync(
        () => Activities.SendBrokerReviewEmail(),
        new() { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) });
}
```

See [`../Patching/MyWorkflow2Patched.workflow.cs`](../Patching/MyWorkflow2Patched.workflow.cs) for the same shape in the sibling sample, and the [Patching](../Patching) sample's README for the full four-stage lifecycle (`Initial → Patched → PatchDeprecated → PatchComplete`).

## SDK version note

This sample runs on the current pinned Temporalio .NET SDK version. The behavior demonstrated is the same on 1.15.0 (which is what the original customer report was against) and on the current version — this is the intended semantics of `Workflow.Patched`, not a bug. `TemporalWorkerOptions.PatchActivationCallback` was added in 1.18.0 to give operators a way to defer patch activation during rolling deploys, but it does **not** rescue a build that has dropped the pre-patch branch — the code fix above is required.
