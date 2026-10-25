namespace ContactsRecord.Domain.Users;

public enum UserRole
{
    /// <summary>Manages users. Only the account from the "SeedUser" configuration holds this role.</summary>
    Admin,

    /// <summary>Works with contacts and imports only.</summary>
    Common,
}
