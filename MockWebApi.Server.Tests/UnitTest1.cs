using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit.Abstractions;

namespace MockWebApi.Server.Tests;

public class UnitTest1 : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _testOutputHelper;

    public UnitTest1(WebApplicationFactory<Program> factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<WebApiOptions>();
                services.AddSingleton<WebApiOptions>(_ => new WebApiOptions()
                {
                    WorkNumber = 5
                });
            });
        });

        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task Test1()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost:7207"),
            AllowAutoRedirect = false,
        });

        var response = await client.PostAsJsonAsync("/jobs/start", new { });
        response.EnsureSuccessStatusCode();

        var startInfo = await response.Content.ReadFromJsonAsync<JobDto>();
        startInfo.ShouldNotBeNull();

        var jobId = startInfo!.Id;

        var handler = _factory.Server.CreateHandler();
        var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');

        var progressEvents = new List<JobState>();
        var completedTcs = new TaskCompletionSource<JobState>();

        var hub = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/progress", options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();


        hub.On<JobState>("progress", state =>
        {
            _testOutputHelper.WriteLine($"Job {state.Id} progress: {state.Current}/{state.Total}");
            progressEvents.Add(state);
        });
        hub.On<JobState>("complete", state => { completedTcs.TrySetResult(state); });

        await hub.StartAsync();
        await hub.InvokeAsync("Join", jobId);

        var completed = await completedTcs.Task;

        progressEvents.ShouldNotBeEmpty("should receive at least one progress event");
        completed.ShouldNotBeNull();
        completed.Id.ShouldBe(jobId);
        completed.Current.ShouldBe(completed.Total);
        progressEvents.Count.ShouldBeEquivalentTo(completed.Total);
        completed.Total.ShouldBe(5);
    }
}

record JobDto(string Id);
