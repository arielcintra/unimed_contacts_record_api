using ContactsRecord.Domain.Imports;

namespace ContactsRecord.Application.Imports.Dtos;

public sealed record ImportRowErrorOutputDto(int LineNumber, ImportRowErrorKind Kind, string Content, string Reason)
{
    public static ImportRowErrorOutputDto From(ImportRowError error) =>
        new(error.LineNumber, error.Kind, error.Content, error.Reason);
}
