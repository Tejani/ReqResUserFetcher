namespace ReqResUserFetcher.Infrastructure.Models;

internal class ApiUserResponse
{
    public int Page { get; set; }
    public int Total { get; set; }
    public int Total_Pages { get; set; }
    public List<ApiUser> Data { get; set; }
}

public class ApiSingleUserResponse
{
    public ApiUser Data { get; set; }
}