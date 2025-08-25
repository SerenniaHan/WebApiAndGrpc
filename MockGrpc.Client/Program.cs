using Grpc.Net.Client;
using GrpcGreeter;

// The port number must match the port of the gRPC server.
using var channel = GrpcChannel.ForAddress("https://localhost:7267");
var client = new Greeter.GreeterClient(channel);
var reply = await client.SayHelloAsync(new HelloRequest { Name = "GreeterClient" });
Console.WriteLine("Greeting: " + reply.Message);

var reply2 = await client.GenerateRandomNumberAsync(new RandomNumberRequest { Number = 1000 });
Console.WriteLine(reply2.Numbers);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();