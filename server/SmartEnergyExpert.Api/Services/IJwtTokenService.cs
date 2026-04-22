using SmartEnergyExpert.Api.Entities;

namespace SmartEnergyExpert.Api.Services;

public interface IJwtTokenService
{
    (string token, int expiresInSeconds) GenerateToken(User user, string roleName);
}
