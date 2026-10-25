using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Imports.Dtos;
using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Application.Imports;

/// <summary>Receives CSV uploads and answers status queries. Processing happens in <see cref="ImportFileService"/>.</summary>
public sealed class ContactImportService(IContactImportRepository imports, IImportQueue queue, TimeProvider clock)
{
    /// <summary>Validates and stores the file, then queues it for asynchronous processing.</summary>
    public async Task<ContactImportOutputDto> StartAsync(
        string? fileName, long length, Stream content, CancellationToken cancellationToken)
    {
        // Validates name and size before reading the content into memory.
        var import = ContactImport.Create(fileName, length, clock.GetUtcNow());

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        await imports.AddAsync(import, buffer.ToArray(), cancellationToken);

        try
        {
            await queue.PublishAsync(import.Id, cancellationToken);
        }
        catch (ImportQueueUnavailableException)
        {
            import.Fail(ImportMessages.QueueUnavailable, clock.GetUtcNow());
            await imports.UpdateAsync(import, cancellationToken);
            throw new ServiceUnavailableException(ImportMessages.QueueUnavailable);
        }

        return ContactImportOutputDto.From(import);
    }

    public async Task<ContactImportOutputDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ContactImportOutputDto.From(await GetExistingAsync(id, cancellationToken));

    public async Task<PagedResultDto<ContactImportOutputDto>> SearchAsync(
        ImportQueryDto query, CancellationToken cancellationToken)
    {
        var result = await imports.SearchAsync(query.Normalize(), cancellationToken);
        return result.Map(ContactImportOutputDto.From);
    }

    public async Task<PagedResultDto<ImportRowErrorOutputDto>> SearchErrorsAsync(
        Guid id, ImportRowErrorQueryDto query, CancellationToken cancellationToken)
    {
        await GetExistingAsync(id, cancellationToken);
        var result = await imports.SearchErrorsAsync(id, query.Normalize(), cancellationToken);
        return result.Map(ImportRowErrorOutputDto.From);
    }

    private async Task<ContactImport> GetExistingAsync(Guid id, CancellationToken cancellationToken) =>
        await imports.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(ImportMessages.NotFound);
}
