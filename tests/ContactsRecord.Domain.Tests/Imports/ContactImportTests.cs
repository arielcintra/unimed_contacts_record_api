using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Domain.Tests.Imports;

public class ContactImportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(1);

    private static ContactImport NewImport() => ContactImport.Create("contatos.csv", 1024, Now);

    private static ContactImport StartedImport()
    {
        var import = NewImport();
        import.Start(Now);
        return import;
    }

    [Fact]
    public void Create_starts_pending()
    {
        var import = NewImport();

        Assert.Equal(ImportStatus.Pending, import.Status);
        Assert.Equal("contatos.csv", import.FileName);
        Assert.Equal(Now, import.CreatedAt);
        Assert.False(import.IsFinished);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("contatos.txt", 10)]
    [InlineData("contatos.csv", 0)]
    [InlineData("contatos.csv", ContactImport.MaxFileSizeBytes + 1)]
    public void Create_rejects_invalid_files(string fileName, long size)
    {
        Assert.Throws<DomainException>(() => ContactImport.Create(fileName, size, Now));
    }

    [Fact]
    public void Create_accepts_extension_in_any_case()
    {
        Assert.Equal("CONTATOS.CSV", ContactImport.Create("CONTATOS.CSV", 10, Now).FileName);
    }

    [Fact]
    public void Batches_accumulate_counters()
    {
        var import = StartedImport();

        import.RecordBatch(imported: 3, duplicates: 1, invalid: 1);
        import.RecordBatch(imported: 2, duplicates: 0, invalid: 0);

        Assert.Equal((7, 5, 1, 1), (import.TotalRows, import.ImportedRows, import.DuplicateRows, import.InvalidRows));
    }

    [Fact]
    public void Complete_without_problem_rows_is_completed()
    {
        var import = StartedImport();
        import.RecordBatch(imported: 2, duplicates: 0, invalid: 0);

        import.Complete(Later);

        Assert.Equal(ImportStatus.Completed, import.Status);
        Assert.Equal(Later, import.FinishedAt);
        Assert.True(import.IsFinished);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void Complete_with_duplicates_or_invalid_rows_is_completed_with_errors(int duplicates, int invalid)
    {
        var import = StartedImport();
        import.RecordBatch(imported: 1, duplicates, invalid);

        import.Complete(Later);

        Assert.Equal(ImportStatus.CompletedWithErrors, import.Status);
    }

    [Fact]
    public void Resume_after_interruption_keeps_counters_and_start_date()
    {
        var import = StartedImport();
        import.RecordBatch(imported: 5, duplicates: 1, invalid: 0);

        import.Start(Later);

        Assert.Equal(ImportStatus.Processing, import.Status);
        Assert.Equal((6, 5, 1), (import.TotalRows, import.ImportedRows, import.DuplicateRows));
        Assert.Equal(Now, import.StartedAt);
    }

    [Fact]
    public void Fail_records_reason()
    {
        var import = StartedImport();

        import.Fail("Cabeçalho inválido.", Later);

        Assert.Equal(ImportStatus.Failed, import.Status);
        Assert.Equal("Cabeçalho inválido.", import.FailureReason);
        Assert.Equal(Later, import.FinishedAt);
    }

    [Fact]
    public void Finished_import_cannot_change_again()
    {
        var import = StartedImport();
        import.Complete(Later);

        Assert.Throws<DomainException>(() => import.Start(Later));
        Assert.Throws<DomainException>(() => import.RecordBatch(1, 0, 0));
        Assert.Throws<DomainException>(() => import.Fail("x", Later));
    }

    [Fact]
    public void Pending_import_cannot_record_or_complete()
    {
        var import = NewImport();

        Assert.Throws<DomainException>(() => import.RecordBatch(1, 0, 0));
        Assert.Throws<DomainException>(() => import.Complete(Later));
    }
}
