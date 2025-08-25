
using BenchmarkDotNet.Running;
using Benchmark;

var summary = BenchmarkRunner.Run<RuntimeApiBenchmark>();

Console.WriteLine("Press any key to continue...");
Console.ReadKey();
