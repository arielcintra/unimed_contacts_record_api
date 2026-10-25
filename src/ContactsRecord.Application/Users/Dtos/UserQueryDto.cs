using ContactsRecord.Application.Common.Dtos;

namespace ContactsRecord.Application.Users.Dtos;

/// <summary>User listing: paging and search by e-mail.</summary>
public sealed record UserQueryDto : PagedQueryDto
{
    public string? Search { get; init; }

    /// <summary>Clamps paging to valid bounds and drops a blank search term.</summary>
    public UserQueryDto Normalize() => this with
    {
        Page = NormalizedPage,
        PageSize = NormalizedPageSize,
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
    };
}
