namespace ContactsRecord.Application.Common.Exceptions;

/// <summary>A required external service (e.g. the message broker) is not reachable.</summary>
public sealed class ServiceUnavailableException(string message) : Exception(message);
