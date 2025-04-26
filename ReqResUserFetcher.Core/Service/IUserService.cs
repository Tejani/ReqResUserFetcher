using ReqResUserFetcher.Core.Models;

namespace ReqResUserFetcher.Core.Service;

public interface IUserService
{
    Task<User> GetUserByIdAsync(int userId);
    Task<IEnumerable<User>> GetAllUsersAsync();
}
