namespace ContactsRecord.Application.Common.Dtos;

/// <summary>Paging parameters shared by every paged listing.</summary>
public abstract record PagedQueryDto
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public int Offset => (Page - 1) * PageSize;

    // Valid bounds for derived Normalize() methods.
    protected int NormalizedPage => Math.Max(1, Page);
    protected int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
