using ContactsRecord.Application.Auth;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;
using ContactsRecord.Infrastructure.Persistence.Dtos;

namespace ContactsRecord.Infrastructure.Persistence;

internal sealed class UserRepository(DbSession session) : IUserRepository
{
    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
    {
        var row = await session.QuerySingleOrDefaultAsync<UserPersistenceDto>(
            "SELECT id, email, password_hash, created_at FROM users WHERE email = @email",
            new { email = email.Value },
            cancellationToken);
        return row?.ToDomain();
    }

    public Task AddAsync(User user, CancellationToken cancellationToken) =>
        session.ExecuteCommandAsync("""
            INSERT INTO users (id, email, password_hash, created_at)
            VALUES (@Id, @Email, @PasswordHash, @CreatedAt)
            ON CONFLICT (email) DO NOTHING
            """, UserPersistenceDto.FromDomain(user), cancellationToken);
}
