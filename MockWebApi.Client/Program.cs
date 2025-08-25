
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

var baseUrl = "https://localhost:7207";
var client = new HttpClient();

// var response = await client.GetAsync($"{baseUil}/generate:{5}/");
// response.EnsureSuccessStatusCode();
// var jsonResponse = await response.Content.ReadAsStringAsync();
// Console.WriteLine(jsonResponse);

/* var taskCompleteSource = new TaskCompletionSource<bool>();

var response = await client.PostAsJsonAsync($"{baseUrl}/jobs/start", new { });

var startInfo = await response.Content.ReadFromJsonAsync<JobDto>();

if (startInfo == null)
{
    Console.WriteLine("Failed to start job.");
    return;
}

Console.WriteLine($"Started job with ID: {startInfo.Id}");

var hub = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/progress")
    .WithAutomaticReconnect()
    .Build();


hub.On<JobState>("progress", state =>
{
    Console.WriteLine($"Job {state.Id} progress: {state.Current}/{state.Total}");
});

hub.On<JobState>("complete", state =>
{
    Console.WriteLine($"Job {state.Id} completed with total: {state.Total}");
    taskCompleteSource.SetResult(true);
});

await hub.StartAsync();

await hub.InvokeAsync("Join", startInfo.Id);

_ = await taskCompleteSource.Task; */

var taskCompleteSource = new TaskCompletionSource<bool>();
var response = await client.PostAsJsonAsync($"{baseUrl}/stage/move", new { });
var startInfo = await response.Content.ReadFromJsonAsync<JobDto>();
if (startInfo == null)
{
    Console.WriteLine("Failed to start stage movement.");
    return;
}

Console.WriteLine($"Started stage movement with ID: {startInfo.Id}");

var hub = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/stage_monitor")
    .WithAutomaticReconnect()
    .Build();

hub.On<AxisInfo>("report", state =>
{
    Console.WriteLine($"Axis {state.Id} report: Position={state.Position:N4}, Velocity={state.Velocity:N4}, Acceleration={state.Acceleration:N4}");
});

hub.On<AxisInfo>("finished", state =>
{
    Console.WriteLine($"Axis {state.Id} finished at Position={state.Position:N4}, Velocity={state.Velocity:N4}, Acceleration={state.Acceleration:N4}");
    taskCompleteSource.SetResult(true);
});

await hub.StartAsync();

await hub.InvokeAsync("Watch", startInfo.Id);

await taskCompleteSource.Task;

Console.WriteLine("Press any key to finish...");
Console.ReadLine();



public record JobDto(string Id);

public class JobState
{
    public string? Id { get; set; }
    public int Total { get; set; }
    public int Current { get; set; }
}

public class AxisInfo
{
    public string Id { get; set; }
    public double Position { get; set; }
    public double Velocity { get; set; }
    public double Acceleration { get; set; }
}