# PatchingNondeterminism Sample

Reproduces `[TMPRL1100] Non-deprecated patch marker encountered ... but there is no corresponding change command` by adding `Workflow.Patched(...)` to an in-flight workflow.

## Steps

1. Ensure line 16 of `MyWorkflowV1Unpatched.workflow.cs` is commented (the `withdrawn = Workflow.Patched(...)` line). This is the V1 build.

2. Start the worker:

       dotnet run worker

3. In another terminal, start a workflow:

       dotnet run starter --start-workflow bad-patch-run-1

   It parks on `WaitConditionAsync`.

4. Stop the worker (Ctrl+C). Uncomment line 16. This is the V2 build.

5. Restart the worker:

       dotnet run worker

6. Send an update (or a signal) to the same workflow:

       dotnet run starter --update-workflow bad-patch-run-1

7. The worker fails the workflow task in a loop with:

       dotnet run starter --proceed-workflow bad-patch-run-1

       [TMPRL1100] Nondeterminism error: Non-deprecated patch marker encountered for change broker-rejection-withdraw-email, but there is no corresponding change command!
