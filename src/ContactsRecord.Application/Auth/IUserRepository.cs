using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Users.Dtos;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;

namespace ContactsRecord.Application.Auth;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <exception cref="ConflictException">The e-mail already belongs to another user.</exception>
    Task AddAsync(User user, CancellationToken cancellationToken);

    /// <exception cref="ConflictException">The e-mail already belongs to another user.</exception>
    Task UpdateAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Makes <paramref name="admin"/> the only administrator: inserts it if its e-mail is not registered
    /// (or gives the existing user the role, keeping its password) and turns any other admin into a common user.
    /// </summary>
    Task EnsureSingleAdminAsync(User admin, CancellationToken cancellationToken);

    /// <param name="query">Already normalized query.</param>
    Task<PagedResultDto<User>> SearchAsync(UserQueryDto query, CancellationToken cancellationToken);
}
