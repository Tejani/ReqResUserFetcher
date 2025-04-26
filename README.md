# ReqResUserFetcher

A .NET 8 component to fetch user data from a public API (`https://reqres.in/api`) with caching, error handling, and retry logic.

## Structure

- `Core` - Interfaces & models
- `Infrastructure` - HttpClient, caching, Polly retry
- `ConsoleApp` - Demo usage
- `Tests` - Unit tests using xUnit

## Features

1. Async HttpClient with retries (Polly)  
2. In-memory caching  
3. Pagination support  
4. Configurable base URL via appsettings  
5. Clean architecture

## How to Run

```bash
dotnet build
dotnet run --project ReqResUserFetcher.ConsoleApp
dotnet test --project ReqResUserFetcher.Tests
