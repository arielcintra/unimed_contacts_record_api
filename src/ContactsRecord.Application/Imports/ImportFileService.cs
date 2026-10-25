using ContactsRecord.Application.Common;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Contacts;
using ContactsRecord.Domain.Imports;
using Microsoft.Extensions.Options;

namespace ContactsRecord.Application.Imports;

/// <summary>
/// Processes an imported CSV file: validates each row with the same rules as the manual form,
/// inserts valid contacts in batches and records every row that was not imported, with the reason.
/// Invoked asynchronously by the message consumer, never by the HTTP request.
/// </summary>
public sealed class ImportFileService(
    IContactImportRepository imports,
    IContactRepository contacts,
    ICsvContactReader reader,
    IUnitOfWork unitOfWork,
    IOptions<ImportOptions> options,
    TimeProvider clock)
{
    public async Task ProcessAsync(Guid importId, CancellationToken cancellationToken)
    {
        var import = await imports.GetByIdAsync(importId, cancellationToken);

        // Unknown or already finished (e.g. the same message delivered twice): nothing to do.
        if (import is null || import.IsFinished) return;

        import.Start(clock.GetUtcNow());
        await imports.UpdateAsync(import, cancellationToken);

        try
        {
            var content = await imports.GetContentAsync(import.Id, cancellationToken);
            await ImportRowsAsync(import, content, cancellationToken);
            import.Complete(clock.GetUtcNow());
        }
        catch (InvalidCsvException ex)
        {
            import.Fail(ex.Message, clock.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The failed batch was rolled back: reload the committed counters before recording the failure.
            var committed = await imports.GetByIdAsync(import.Id, CancellationToken.None) ?? import;
            committed.Fail(ImportMessages.UnexpectedFailure, clock.GetUtcNow());
            await imports.UpdateAsync(committed, CancellationToken.None);
            throw;
        }

        await imports.UpdateAsync(import, cancellationToken);
    }

    private async Task ImportRowsAsync(ContactImport import, byte[] content, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content, writable: false);
        var batchSize = options.Value.BatchSize;
        var firstLineByPhone = new Dictionary<string, int>();
        var alreadyProcessed = import.TotalRows;
        var batch = new List<CsvContactRow>(batchSize);

        foreach (var row in reader.Read(stream))
        {
            // Resuming an interrupted import: rows of committed batches are skipped, but their phones are
            // remembered so a later repetition in the file is still reported as a duplicate.
            if (alreadyProcessed > 0)
            {
                alreadyProcessed--;
                if (TryCreateContact(row, out var processed)) firstLineByPhone.TryAdd(processed.Phone.Value, row.LineNumber);
                continue;
            }

            batch.Add(row);
            if (batch.Count < batchSize) continue;

            await ImportBatchAsync(import, batch, firstLineByPhone, cancellationToken);
            batch.Clear();
        }

        if (batch.Count > 0)
            await ImportBatchAsync(import, batch, firstLineByPhone, cancellationToken);
    }

    private async Task ImportBatchAsync(
        ContactImport import,
        IReadOnlyList<CsvContactRow> rows,
        Dictionary<string, int> firstLineByPhone,
        CancellationToken cancellationToken)
    {
        var errors = new List<ImportRowError>();
        var candidates = new List<(CsvContactRow Row, Contact Contact)>();

        foreach (var row in rows)
        {
            if (!TryCreateContact(row, out var contact, out var invalidReason))
            {
                errors.Add(new ImportRowError(row.LineNumber, ImportRowErrorKind.Invalid, row.Content, invalidReason));
                continue;
            }

            var phone = contact.Phone.Value;
            if (firstLineByPhone.TryGetValue(phone, out var firstLine))
            {
                errors.Add(Duplicate(row, ImportMessages.DuplicateInFile(phone, firstLine)));
                continue;
            }

            firstLineByPhone[phone] = row.LineNumber;
            candidates.Add((row, contact));
        }

        // Unit of work: contacts, errors and counters of the batch are saved together or not at all,
        // so the counters always match the data and an interrupted import can resume after the last batch.
        await unitOfWork.ExecuteAsync(async () =>
        {
            var inserted = await contacts.AddManyIgnoringDuplicatesAsync(
                candidates.Select(c => c.Contact).ToList(), cancellationToken);

            var skipped = candidates.Where(c => !inserted.Contains(c.Contact.Phone.Value)).ToList();
            if (skipped.Count > 0)
                errors.AddRange(await DescribeExistingDuplicatesAsync(skipped, cancellationToken));

            await imports.AddErrorsAsync(import.Id, errors, cancellationToken);

            import.RecordBatch(
                imported: inserted.Count,
                duplicates: errors.Count(e => e.Kind == ImportRowErrorKind.Duplicate),
                invalid: errors.Count(e => e.Kind == ImportRowErrorKind.Invalid));
            await imports.UpdateAsync(import, cancellationToken);
        }, cancellationToken);
    }

    private bool TryCreateContact(CsvContactRow row, out Contact contact) => TryCreateContact(row, out contact, out _);

    private bool TryCreateContact(CsvContactRow row, out Contact contact, out string invalidReason)
    {
        try
        {
            contact = row.Contact.ToNewContact(clock.GetUtcNow());
            invalidReason = string.Empty;
            return true;
        }
        catch (DomainException ex)
        {
            contact = null!;
            invalidReason = ex.Message;
            return false;
        }
    }

    // Rows skipped by the database because the phone already exists: name the contact that owns it.
    private async Task<IEnumerable<ImportRowError>> DescribeExistingDuplicatesAsync(
        List<(CsvContactRow Row, Contact Contact)> skipped, CancellationToken cancellationToken)
    {
        var owners = (await contacts.GetByPhonesAsync(skipped.Select(s => s.Contact.Phone).ToList(), cancellationToken))
            .ToDictionary(owner => owner.Phone.Value);

        return skipped.Select(s =>
        {
            var phone = s.Contact.Phone.Value;
            var reason = owners.TryGetValue(phone, out var owner)
                ? ImportMessages.DuplicateOfContact(phone, owner.Name, owner.IsActive)
                : ImportMessages.DuplicateOfUnknownContact(phone);
            return Duplicate(s.Row, reason);
        });
    }

    private static ImportRowError Duplicate(CsvContactRow row, string reason) =>
        new(row.LineNumber, ImportRowErrorKind.Duplicate, row.Content, reason);
}
