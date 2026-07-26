using TaskManagerAPI.Models;

namespace TaskManagerAPI.Services;

public interface ITokenService
{
    string CreateToken(User user);
}
