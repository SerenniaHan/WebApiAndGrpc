using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Grpc.Net.Client;
using GrpcGreeter;

namespace Benchmark;

[SimpleJob(RuntimeMoniker.Net90)]
public class RuntimeApiBenchmark
{
    private readonly HttpClient _webApiClient = new();
    private readonly Greeter.GreeterClient _grpcClient = new(GrpcChannel.ForAddress("https://localhost:7267"));


    [Benchmark]
    public async Task WebApiBenchmark()
    {
        var response = await _webApiClient.GetAsync($"https://localhost:7207/generate:{10000}/");
        _ = response.EnsureSuccessStatusCode();
        _ = await response.Content.ReadAsStringAsync();
    }

    [Benchmark]
    public async Task GrpcApiBenchmark()
    {
        _ = await _grpcClient.GenerateRandomNumberAsync(new RandomNumberRequest { Number = 10000 });
    }
}
