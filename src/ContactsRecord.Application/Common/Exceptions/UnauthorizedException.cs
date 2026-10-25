namespace ContactsRecord.Application.Common.Exceptions;

public sealed class UnauthorizedException(string message) : Exception(message);
