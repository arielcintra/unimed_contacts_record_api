using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Application.Imports.Dtos;

/// <summary>Rows not imported, optionally filtered by kind (Invalid or Duplicate).</summary>
public sealed record ImportRowErrorQueryDto : PagedQueryDto
{
    public ImportRowErrorKind? Kind { get; init; }

    public ImportRowErrorQueryDto Normalize() => this with { Page = NormalizedPage, PageSize = NormalizedPageSize };
}
