namespace FHGM.Admin.ViewModels;

public sealed record LoginViewModel(string ReturnUrl);
public sealed record DashboardViewModel(string ActiveView);
public sealed record SessionViewModel(string Id, string Email, string DisplayName, string[] Roles);
