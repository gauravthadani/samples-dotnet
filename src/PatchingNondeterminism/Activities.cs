using Temporalio.Activities;

namespace TemporalioSamples.PatchingNondeterminism;

public static class Activities
{
    [Activity]
    public static string SendBrokerReviewEmail() => "sent broker review email";

    [Activity]
    public static string WithdrawBrokerRejectionEmail() => "withdrew broker rejection email";
}
