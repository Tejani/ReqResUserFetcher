using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReqResUserFetcher.Core.Service;
using ReqResUserFetcher.Infrastructure.Configuration;
using ReqResUserFetcher.Infrastructure.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json");
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<ReqResOptions>(context.Configuration.GetSection("ReqResApi"));
        services.AddMemoryCache();
        services.AddHttpClient<UserService>();
        services.AddScoped<IUserService, UserService>();
    })
    .Build();

var userService = host.Services.GetRequiredService<IUserService>();
var user = await userService.GetUserByIdAsync(2);
Console.WriteLine($"User 2: {user.FirstName} {user.LastName} - {user.Email}");

var users = await userService.GetAllUsersAsync();
Console.WriteLine($"Fetched {users.Count()} users.");

Console.WriteLine("----");

foreach (var u in users)
{
    Console.WriteLine($"- {u.FirstName} {u.LastName}");
}