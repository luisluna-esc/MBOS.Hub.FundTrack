namespace FundTrack.core.Constants;

public static class SubmissionStatuses
{
    public const string Submitted = "submitted";
    /// <summary>La iglesia no presentó arqueo ese mes.</summary>
    public const string NotSubmitted = "not_submitted";

    public static readonly string[] All = [Submitted, NotSubmitted];
}
