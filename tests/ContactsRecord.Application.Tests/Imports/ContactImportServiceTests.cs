using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Imports;
using ContactsRecord.Application.Imports.Dtos;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Imports;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ContactsRecord.Application.Tests.Imports;

public class ContactImportServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Csv = "nome,email,telefone\nAna,,11987654321"u8.ToArray();

    private readonly IContactImportRepository _imports = Substitute.For<IContactImportRepository>();
    private readonly IImportQueue _queue = Substitute.For<IImportQueue>();
    private readonly ContactImportService _service;

    public ContactImportServiceTests()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        _service = new ContactImportService(_imports, _queue, clock);
    }

    private Task<ContactImportOutputDto> UploadAsync(string fileName = "contatos.csv") =>
        _service.StartAsync(fileName, Csv.Length, new MemoryStream(Csv), CancellationToken.None);

    [Fact]
    public async Task Upload_stores_the_file_and_queues_it()
    {
        var output = await UploadAsync();

        Assert.Equal(ImportStatus.Pending, output.Status);
        await _imports.Received(1).AddAsync(
            Arg.Is<ContactImport>(i => i.Id == output.Id),
            Arg.Is<byte[]>(content => content.SequenceEqual(Csv)),
            Arg.Any<CancellationToken>());
        await _queue.Received(1).PublishAsync(output.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_of_invalid_file_stores_and_queues_nothing()
    {
        await Assert.ThrowsAsync<DomainException>(() => UploadAsync("contatos.txt"));

        Assert.Empty(_imports.ReceivedCalls());
        Assert.Empty(_queue.ReceivedCalls());
    }

    [Fact]
    public async Task Upload_with_queue_unavailable_marks_import_as_failed()
    {
        _queue.PublishAsync(default, default)
            .ThrowsAsyncForAnyArgs(new ImportQueueUnavailableException(new Exception("down")));

        var error = await Assert.ThrowsAsync<ServiceUnavailableException>(() => UploadAsync());

        Assert.Equal(ImportMessages.QueueUnavailable, error.Message);
        await _imports.Received(1).UpdateAsync(
            Arg.Is<ContactImport>(i => i.Status == ImportStatus.Failed && i.FailureReason == ImportMessages.QueueUnavailable),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_of_unknown_import_fails()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Errors_of_unknown_import_fail()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SearchErrorsAsync(Guid.NewGuid(), new ImportRowErrorQueryDto(), CancellationToken.None));
    }
}
