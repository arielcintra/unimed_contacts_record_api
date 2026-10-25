using ContactsRecord.Application.Auth.Dtos;
using ContactsRecord.Domain.Users;

namespace ContactsRecord.Application.Auth;

public interface ITokenService
{
    LoginOutputDto Generate(User user);
}
