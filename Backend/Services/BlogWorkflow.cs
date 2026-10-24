using FHGM.Backend.Entities;

namespace FHGM.Backend.Services;

// Role/ownership checks are performed by the controller; this defines legal state changes.
public static class BlogWorkflow
{
    public static BlogStatus? Next(BlogStatus current, string action) => (current, action) switch
    {
        (BlogStatus.Draft or BlogStatus.ChangesRequested or BlogStatus.Rejected, "submit") => BlogStatus.Submitted,
        (BlogStatus.Submitted, "request-changes") => BlogStatus.ChangesRequested,
        (BlogStatus.Submitted, "reject") => BlogStatus.Rejected,
        (BlogStatus.Submitted, "approve") => BlogStatus.Approved,
        (BlogStatus.Approved, "publish") => BlogStatus.Published,
        (BlogStatus.Published or BlogStatus.Approved, "archive") => BlogStatus.Archived,
        (BlogStatus.Archived, "restore") => BlogStatus.Draft,
        _ => null
    };
    public static bool CanEdit(BlogStatus status) => status is BlogStatus.Draft or BlogStatus.ChangesRequested or BlogStatus.Rejected or BlogStatus.Submitted;
}
