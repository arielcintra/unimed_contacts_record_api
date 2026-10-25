using ContactsRecord.Application.Common.Dtos;

namespace ContactsRecord.Application.Contacts.Dtos;

/// <summary>Contact listing: paging, search by name/e-mail/phone and favorite/status filters.</summary>
public sealed record ContactQueryDto : PagedQueryDto
{
    public string? Search { get; init; }
    public bool? IsFavorite { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>Clamps paging to valid bounds and drops a blank search term.</summary>
    public ContactQueryDto Normalize() => this with
    {
        Page = NormalizedPage,
        PageSize = NormalizedPageSize,
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
    };
}
