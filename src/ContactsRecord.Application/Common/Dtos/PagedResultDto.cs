namespace ContactsRecord.Application.Common.Dtos;

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public PagedResultDto<TResult> Map<TResult>(Func<T, TResult> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}
