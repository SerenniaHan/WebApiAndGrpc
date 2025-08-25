using Grpc.Core;
using GrpcGreeter;

namespace GrpcGreeter.Services;

public class GreeterService : Greeter.GreeterBase
{
    private readonly ILogger<GreeterService> _logger;
    public GreeterService(ILogger<GreeterService> logger)
    {
        _logger = logger;
    }

    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name
        });
    }

    public override Task<RandomNumberReply> GenerateRandomNumber(RandomNumberRequest request, ServerCallContext context)
    {
        var random = new Random();
        var numbers = Enumerable.Range(0, request.Number).Select(_ => (float)random.Next()).ToList();
        return Task.FromResult(new RandomNumberReply
        {
            Numbers = { numbers }
        });
    }
}
