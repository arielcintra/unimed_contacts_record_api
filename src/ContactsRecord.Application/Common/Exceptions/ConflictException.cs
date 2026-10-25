namespace ContactsRecord.Application.Common.Exceptions;

/// <summary>The request conflicts with the current state of the data (e.g. duplicated phone).</summary>
public sealed class ConflictException(string message) : Exception(message);
