using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Imports.Dtos;
using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Application.Imports;

public interface IContactImportRepository
{
    /// <summary>Stores the import together with the uploaded file content.</summary>
    Task AddAsync(ContactImport import, byte[] content, CancellationToken cancellationToken);

    Task<ContactImport?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<byte[]> GetContentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Saves status, counters and dates.</summary>
    Task UpdateAsync(ContactImport import, CancellationToken cancellationToken);

    /// <param name="query">Already normalized query.</param>
    Task<PagedResultDto<ContactImport>> SearchAsync(ImportQueryDto query, CancellationToken cancellationToken);

    Task AddErrorsAsync(Guid importId, IReadOnlyCollection<ImportRowError> errors, CancellationToken cancellationToken);

    /// <param name="query">Already normalized query.</param>
    Task<PagedResultDto<ImportRowError>> SearchErrorsAsync(
        Guid importId, ImportRowErrorQueryDto query, CancellationToken cancellationToken);
}
