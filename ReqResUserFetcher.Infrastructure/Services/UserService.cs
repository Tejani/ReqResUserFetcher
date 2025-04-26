using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using ReqResUserFetcher.Core.Models;
using ReqResUserFetcher.Core.Service;
using ReqResUserFetcher.Infrastructure.Configuration;
using ReqResUserFetcher.Infrastructure.Models;
using System.Net.Http.Json;

namespace ReqResUserFetcher.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly string _baseUrl;
    private readonly ILogger<UserService> _logger;

    public UserService(HttpClient httpClient, IMemoryCache cache, IOptions<ReqResOptions> options, ILogger<UserService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _baseUrl = options.Value.BaseUrl;

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        _logger = logger;
    }
    public async Task<User> GetUserByIdAsync(int userId)
    {
        try
        {
            string cacheKey = $"user:{userId}";
            if (_cache.TryGetValue(cacheKey, out User user))
                return user;

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/users/{userId}");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    _logger.LogWarning("User with ID {UserId} not found.", userId);

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiSingleUserResponse>(content);
                var apiUser = result?.Data;

                user = new User
                {
                    Id = apiUser.Id,
                    Email = apiUser.Email,
                    FirstName = apiUser.First_Name,
                    LastName = apiUser.Last_Name,
                    Avatar = apiUser.Avatar
                };

                _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));
                return user;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user with ID {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        try
        {
            string cacheKey = "all_users";
            if (_cache.TryGetValue(cacheKey, out List<User> allUsers))
                return allUsers;

            allUsers = new List<User>();
            int page = 1;
            bool hasMore = true;

            while (hasMore)
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/users?page={page}");
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadFromJsonAsync<ApiUserResponse>();
                if (data?.Data == null || !data.Data.Any())
                    break;

                allUsers.AddRange(data.Data.Select(u => new User
                {
                    Id = u.Id,
                    Email = u.Email,
                    FirstName = u.First_Name,
                    LastName = u.Last_Name,
                    Avatar = u.Avatar
                }));

                hasMore = page < data.Total_Pages;
                page++;
            }

            _cache.Set(cacheKey, allUsers, TimeSpan.FromMinutes(10));
            return allUsers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users list.");
            throw;
        }
    }
}
