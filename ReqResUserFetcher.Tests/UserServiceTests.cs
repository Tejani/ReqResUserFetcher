using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ReqResUserFetcher.Infrastructure.Configuration;
using ReqResUserFetcher.Infrastructure.Services;
using System.Net;
using System.Net.Http.Json;

namespace ReqResUserFetcher.Tests;

public class UserServiceTests
{
    private UserService CreateService(HttpResponseMessage responseMessage)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);

        var httpClient = new HttpClient(handlerMock.Object);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var optionsMock = new Mock<IOptions<ReqResOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new ReqResOptions
        {
            BaseUrl = "https://reqres.in/api"
        });

        var loggerMock = new Mock<ILogger<UserService>>();

        return new UserService(httpClient, memoryCache, optionsMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task GetUserByIdAsync_ReturnsUser_WhenSuccessful()
    {
        // Arrange
        var responseContent = new
        {
            data = new
            {
                id = 1,
                email = "test@example.com",
                first_name = "Test",
                last_name = "User",
                avatar = "https://example.com/avatar.jpg"
            }
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var service = CreateService(response);

        // Act
        var user = await service.GetUserByIdAsync(1);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(1, user.Id);
        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.Equal("test@example.com", user.Email);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsUsers_WhenSuccessful()
    {
        // Arrange
        var responseContentPage1 = new
        {
            page = 1,
            total_pages = 1,
            data = new[]
            {
                new
                {
                    id = 1,
                    email = "test1@example.com",
                    first_name = "Test1",
                    last_name = "User1",
                    avatar = "https://example.com/avatar1.jpg"
                },
                new
                {
                    id = 2,
                    email = "test2@example.com",
                    first_name = "Test2",
                    last_name = "User2",
                    avatar = "https://example.com/avatar2.jpg"
                }
            }
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContentPage1)
        };

        var service = CreateService(response);

        // Act
        var users = await service.GetAllUsersAsync();

        // Assert
        Assert.NotNull(users);
        Assert.Equal(2, users.Count());
        Assert.Contains(users, u => u.Id == 1);
        Assert.Contains(users, u => u.Id == 2);
    }

    [Fact]
    public async Task GetUserByIdAsync_ThrowsException_WhenUserNotFound()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        var service = CreateService(response);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () => await service.GetUserByIdAsync(999));
    }

    [Fact]
    public async Task GetUserByIdAsync_UsesCache_OnSecondCall()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = new
                    {
                        id = 1,
                        email = "cached@example.com",
                        first_name = "Cached",
                        last_name = "User",
                        avatar = "https://example.com/avatar.jpg"
                    }
                })
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var optionsMock = new Mock<IOptions<ReqResOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new ReqResOptions { BaseUrl = "https://reqres.in/api" });

        var loggerMock = new Mock<ILogger<UserService>>();
        var service = new UserService(httpClient, memoryCache, optionsMock.Object, loggerMock.Object);

        // Act
        var userFirstCall = await service.GetUserByIdAsync(1);
        var userSecondCall = await service.GetUserByIdAsync(1);

        // Assert
        handlerMock.Protected()
            .Verify(
                "SendAsync",
                Times.Once(), // Only ONE HTTP call should happen (second call from cache)
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );

        Assert.Equal(userFirstCall.Id, userSecondCall.Id);
    }

    [Fact]
    public async Task GetUserByIdAsync_Retries_OnTransientFailure()
    {
        // Arrange
        int callCount = 0;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new HttpRequestException("Transient network error");
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        data = new
                        {
                            id = 5,
                            email = "retry@example.com",
                            first_name = "Retry",
                            last_name = "Success",
                            avatar = "https://example.com/avatar5.jpg"
                        }
                    })
                };
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var optionsMock = new Mock<IOptions<ReqResOptions>>();
        optionsMock.Setup(o => o.Value).Returns(new ReqResOptions { BaseUrl = "https://reqres.in/api" });

        var loggerMock = new Mock<ILogger<UserService>>();

        var service = new UserService(httpClient, memoryCache, optionsMock.Object, loggerMock.Object);

        // Act
        var user = await service.GetUserByIdAsync(5);

        // Assert
        Assert.Equal(5, user.Id);
        Assert.Equal(3, callCount); // It retried twice, succeeded on 3rd attempt
    }

}
