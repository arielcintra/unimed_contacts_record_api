using ContactsRecord.Application.Common.Dtos;

namespace ContactsRecord.Application.Imports.Dtos;

/// <summary>Import history listing (most recent first).</summary>
public sealed record ImportQueryDto : PagedQueryDto
{
    public ImportQueryDto Normalize() => this with { Page = NormalizedPage, PageSize = NormalizedPageSize };
}
