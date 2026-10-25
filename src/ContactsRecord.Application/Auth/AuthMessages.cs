namespace ContactsRecord.Application.Auth;

public static class AuthMessages
{
    // Same message for unknown e-mail and wrong password, so registered e-mails can't be discovered.
    public const string InvalidCredentials = "E-mail ou senha inválidos.";
}
