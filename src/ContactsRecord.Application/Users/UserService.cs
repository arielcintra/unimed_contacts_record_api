using ContactsRecord.Application.Auth;
using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Users.Dtos;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Users;

namespace ContactsRecord.Application.Users;

/// <summary>User management. Restricted to the administrator by the API (role-based authorization).</summary>
public sealed class UserService(IUserRepository users, IPasswordHasher passwordHasher, TimeProvider clock)
{
    public async Task<PagedResultDto<UserOutputDto>> SearchAsync(UserQueryDto query, CancellationToken cancellationToken)
    {
        var result = await users.SearchAsync(query.Normalize(), cancellationToken);
        return result.Map(UserOutputDto.From);
    }

    public async Task<UserOutputDto> CreateAsync(UserInputDto input, CancellationToken cancellationToken)
    {
        var email = Email.Create(input.Email);
        User.EnsureValidPassword(input.Password);

        var user = User.Create(email, passwordHasher.Hash(input.Password!), clock.GetUtcNow());

        await EnsureEmailIsAvailableAsync(user, cancellationToken);
        await users.AddAsync(user, cancellationToken);

        return UserOutputDto.From(user);
    }

    public async Task<UserOutputDto> UpdateAsync(Guid id, UserInputDto input, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(UserMessages.NotFound);
        var email = Email.Create(input.Email);

        string? passwordHash = null;
        if (!string.IsNullOrEmpty(input.Password))
        {
            User.EnsureValidPassword(input.Password);
            passwordHash = passwordHasher.Hash(input.Password);
        }

        user.Update(email, passwordHash);

        await EnsureEmailIsAvailableAsync(user, cancellationToken);
        await users.UpdateAsync(user, cancellationToken);

        return UserOutputDto.From(user);
    }

    // Friendly pre-check; the unique index on e-mail is the real guarantee against concurrent requests.
    private async Task EnsureEmailIsAvailableAsync(User user, CancellationToken cancellationToken)
    {
        var owner = await users.GetByEmailAsync(user.Email, cancellationToken);
        if (owner is not null && owner.Id != user.Id) throw new ConflictException(UserMessages.EmailInUse);
    }
}
