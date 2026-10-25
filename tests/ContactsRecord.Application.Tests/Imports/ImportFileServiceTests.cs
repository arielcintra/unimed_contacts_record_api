using ContactsRecord.Application.Common;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Application.Imports;
using ContactsRecord.Domain.Contacts;
using ContactsRecord.Domain.Imports;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.Extensions;

namespace ContactsRecord.Application.Tests.Imports;

public class ImportFileServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly IContactImportRepository _imports = Substitute.For<IContactImportRepository>();
    private readonly IContactRepository _contacts = Substitute.For<IContactRepository>();
    private readonly ICsvContactReader _reader = Substitute.For<ICsvContactReader>();
    private readonly TimeProvider _clock = Substitute.For<TimeProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<ImportRowError> _errors = [];
    private readonly List<Contact> _existingContacts = [];
    private readonly ContactImport _import = ContactImport.Create("contatos.csv", 100, Now);

    public ImportFileServiceTests()
    {
        _clock.GetUtcNow().Returns(Now);
        // Runs the work immediately, as a committed transaction would.
        _unitOfWork.ExecuteAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task>>()());
        _imports.GetByIdAsync(_import.Id, Arg.Any<CancellationToken>()).Returns(_import);
        _imports.GetContentAsync(_import.Id, Arg.Any<CancellationToken>()).Returns([]);
        _imports.AddErrorsAsync(_import.Id, Arg.Do<IReadOnlyCollection<ImportRowError>>(_errors.AddRange), Arg.Any<CancellationToken>());

        // The database inserts every contact whose phone isn't already registered.
        _contacts.AddManyIgnoringDuplicatesAsync(Arg.Any<IReadOnlyCollection<Contact>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlySet<string>)call.Arg<IReadOnlyCollection<Contact>>()
                .Select(c => c.Phone.Value)
                .Except(_existingContacts.Select(c => c.Phone.Value))
                .ToHashSet());
        _contacts.GetByPhonesAsync(Arg.Any<IReadOnlyCollection<PhoneNumber>>(), Arg.Any<CancellationToken>())
            .Returns(_ => _existingContacts);
    }

    private ImportFileService Service(int batchSize = 500) => new(
        _imports, _contacts, _reader, _unitOfWork, Options.Create(new ImportOptions { BatchSize = batchSize }), _clock);

    private void GivenRows(params CsvContactRow[] rows) =>
        _reader.Read(Arg.Any<Stream>()).Returns(rows);

    private static CsvContactRow Row(int line, string name, string phone, string? email = null) =>
        new(line, $"{name},{email},{phone}", new ContactInputDto(name, email, phone));

    private Task ProcessAsync(int batchSize = 500) => Service(batchSize).ProcessAsync(_import.Id, CancellationToken.None);

    [Fact]
    public async Task Valid_rows_are_imported()
    {
        GivenRows(Row(2, "Ana", "11987650001"), Row(3, "Bia", "(11) 98765-0002"));

        await ProcessAsync();

        Assert.Equal(ImportStatus.Completed, _import.Status);
        Assert.Equal((2, 2, 0, 0), (_import.TotalRows, _import.ImportedRows, _import.DuplicateRows, _import.InvalidRows));
        Assert.Empty(_errors);
    }

    [Fact]
    public async Task Invalid_row_is_reported_with_line_content_and_reason()
    {
        var invalid = Row(3, "Bia", "123");
        GivenRows(Row(2, "Ana", "11987650001"), invalid);

        await ProcessAsync();

        var error = Assert.Single(_errors);
        Assert.Equal((3, ImportRowErrorKind.Invalid, invalid.Content), (error.LineNumber, error.Kind, error.Content));
        Assert.StartsWith("Telefone inválido", error.Reason);
        Assert.Equal(ImportStatus.CompletedWithErrors, _import.Status);
        Assert.Equal((1, 1), (_import.ImportedRows, _import.InvalidRows));
    }

    [Fact]
    public async Task Phone_repeated_in_the_file_is_a_duplicate_of_its_first_line()
    {
        GivenRows(Row(2, "Ana", "11987650001"), Row(5, "Ana de novo", "+55 11 98765-0001"));

        await ProcessAsync();

        var error = Assert.Single(_errors);
        Assert.Equal((5, ImportRowErrorKind.Duplicate), (error.LineNumber, error.Kind));
        Assert.Equal(ImportMessages.DuplicateInFile("11987650001", firstLine: 2), error.Reason);
        Assert.Equal((1, 1), (_import.ImportedRows, _import.DuplicateRows));
    }

    [Fact]
    public async Task Phone_already_registered_is_a_duplicate_naming_the_existing_contact()
    {
        var existing = Contact.Create("Carla", null, PhoneNumber.Create("11987650001"), Now);
        existing.Deactivate(Now);
        _existingContacts.Add(existing);
        GivenRows(Row(2, "Ana", "11987650001"));

        await ProcessAsync();

        var error = Assert.Single(_errors);
        Assert.Equal(ImportRowErrorKind.Duplicate, error.Kind);
        Assert.Equal(ImportMessages.DuplicateOfContact("11987650001", "Carla", isActive: false), error.Reason);
        Assert.Equal((0, 1), (_import.ImportedRows, _import.DuplicateRows));
    }

    [Fact]
    public async Task Rows_are_inserted_in_batches_of_the_configured_size()
    {
        GivenRows(Enumerable.Range(0, 5).Select(i => Row(i + 2, $"C{i}", $"1198765000{i}")).ToArray());

        await ProcessAsync(batchSize: 2);

        await _contacts.Received(3).AddManyIgnoringDuplicatesAsync(
            Arg.Any<IReadOnlyCollection<Contact>>(), Arg.Any<CancellationToken>());
        Assert.Equal(5, _import.ImportedRows);
    }

    [Fact]
    public async Task Each_batch_is_saved_in_its_own_unit_of_work()
    {
        GivenRows(Enumerable.Range(0, 5).Select(i => Row(i + 2, $"C{i}", $"1198765000{i}")).ToArray());

        await ProcessAsync(batchSize: 2);

        await _unitOfWork.Received(3).ExecuteAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Interrupted_import_resumes_after_the_rows_already_processed()
    {
        // First attempt committed the batch with lines 2 and 3, then the API stopped.
        _import.Start(Now);
        _import.RecordBatch(imported: 2, duplicates: 0, invalid: 0);
        GivenRows(
            Row(2, "Ana", "11987650001"),
            Row(3, "Bia", "11987650002"),
            Row(4, "Carla", "11987650003"),
            Row(5, "Ana de novo", "11987650001"));

        await ProcessAsync();

        await _contacts.Received(1).AddManyIgnoringDuplicatesAsync(
            Arg.Is<IReadOnlyCollection<Contact>>(batch => batch.Single().Name == "Carla"), Arg.Any<CancellationToken>());
        var error = Assert.Single(_errors);
        Assert.Equal(ImportMessages.DuplicateInFile("11987650001", firstLine: 2), error.Reason);
        Assert.Equal((ImportStatus.CompletedWithErrors, 4, 3, 1),
            (_import.Status, _import.TotalRows, _import.ImportedRows, _import.DuplicateRows));
    }

    [Fact]
    public async Task Unreadable_csv_fails_the_import_with_the_reason()
    {
        _reader.Read(Arg.Any<Stream>()).Throws(new InvalidCsvException("Cabeçalho inválido."));

        await ProcessAsync();

        Assert.Equal(ImportStatus.Failed, _import.Status);
        Assert.Equal("Cabeçalho inválido.", _import.FailureReason);
    }

    [Fact]
    public async Task Unexpected_error_fails_the_import_and_is_rethrown()
    {
        GivenRows(Row(2, "Ana", "11987650001"));
        _contacts.Configure().AddManyIgnoringDuplicatesAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new TimeoutException());

        await Assert.ThrowsAsync<TimeoutException>(() => ProcessAsync());

        Assert.Equal(ImportStatus.Failed, _import.Status);
        Assert.Equal(ImportMessages.UnexpectedFailure, _import.FailureReason);
    }

    [Fact]
    public async Task Already_finished_import_is_ignored()
    {
        _import.Start(Now);
        _import.Complete(Now);

        await ProcessAsync();

        _reader.DidNotReceiveWithAnyArgs().Read(default!);
        await _imports.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }
}
