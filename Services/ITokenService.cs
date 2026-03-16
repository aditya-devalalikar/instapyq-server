using pqy_server.Models.Users;

public interface ITokenService
{
    string CreateAccessToken(User user);
    string CreateRefreshToken();
}
