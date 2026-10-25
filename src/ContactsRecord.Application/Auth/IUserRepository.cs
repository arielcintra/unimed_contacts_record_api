using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;

namespace ContactsRecord.Application.Auth;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>Stores the user; does nothing if the e-mail is already registered.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken);
}
