using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;

Console.WriteLine("Start Web Api server");
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<JobStore>();
builder.Services.AddSignalR();

builder.Services.AddSingleton<WebApiOptions>(_ => new WebApiOptions()
{
    WorkNumber = 20
});

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHub<ProgressHub>("/hubs/progress");
app.MapHub<StageReporterHub>("/hubs/stage_monitor");

app.UseHttpsRedirection();

app.MapGet("/generate:{number}", (int number) =>
{
    var random = new Random();
    var numbers = Enumerable.Range(0, number).Select(_ => (float)random.NextDouble()).ToArray();
    return numbers;
}).WithName("Generate");

app.MapPost("/generatepost", async (HttpContext context) =>
{
    var requestData = await context.Request.ReadFromJsonAsync<GenerateRequest>();
    if (requestData == null) return Results.BadRequest("Invalid request data");

    var random = new Random();
    var numbers = Enumerable.Range(0, requestData.Number).Select(_ => (float)random.NextDouble()).ToArray();
    return Results.Json(numbers);
}).WithName("GeneratePostWay");

app.MapPost("/jobs/start", (WebApiOptions options, JobStore store, IHubContext<ProgressHub> hubContext) =>
{
    var id = Guid.NewGuid().ToString();
    var random = new Random();
    var total = options.WorkNumber;
    store.Create(id, total);

    _ = Task.Run(async () =>
    {
        for (int i = 0; i < total; i++)
        {
            await Task.Delay(1000);
            await hubContext.Clients.Group(id).SendAsync("progress", store.Report(id, i + 1));
        }

        await hubContext.Clients.Group(id).SendAsync("complete", store.Complete(id));
    });

    return Results.Ok(new { Id = id });
});

// stage s-curve control
app.MapPost("/stage/move", (StageControl stageControl, IHubContext<StageReporterHub> hubContext) =>
{
    var id = Guid.NewGuid().ToString();
    var current = 0.0d;
    var target = 100.0d;
    var dt = 0.1d;
    var minVelocity = 0.0001d;
    var maxVelocity = 30.0d;
    var minAcceleration = 0.001d;
    var maxAcceleration = 5.0d;
    var maxJerk = 2.0d;
    var currentVelocity = 0.0d;
    var currentAcceleration = 0.0d;
    var distance = Math.Abs(target - current);

    stageControl.Create(id);

    // move axis from 0 to 100.0 position at speed 10.0
    _ = Task.Run(async () =>
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(dt));
            var remain = Math.Abs(target - current);
            var direction = Math.Sign(target - current);
            var deceDistance = (currentVelocity * currentVelocity) / (2 * maxAcceleration);

            if (remain <= 0)
            {
                break;
            }

            if (deceDistance >= remain)
            {
                if (currentAcceleration > -maxAcceleration)
                {
                    currentAcceleration -= maxJerk * dt;
                }
                else
                {
                    if (currentAcceleration < maxAcceleration)
                    {
                        currentAcceleration += maxJerk * dt;
                    }
                }
            }

            currentVelocity += currentAcceleration * dt;
            if (currentVelocity >= maxVelocity)
            {
                currentVelocity = maxVelocity;
            }
            if (currentVelocity < minVelocity)
            {
                currentVelocity = minVelocity;
            }

            current += direction * currentVelocity * dt;
            await hubContext.Clients.Group(id).SendAsync("report", stageControl.Report(id, current, currentVelocity, currentAcceleration));
            if (remain < 0.001 && currentVelocity < 0.001)
            {
                break;
            }
        }

        current = target;
        currentVelocity = 0.0d;
        currentAcceleration = 0.0d;

        await hubContext.Clients.Group(id).SendAsync("finished", stageControl.Finished(id, current, currentVelocity, currentAcceleration));
    });
    return Results.Ok(new { Id = id });
});

app.Run();

public record GenerateRequest(int Number);


public class ProgressHub : Hub
{
    public Task Join(string id) => Groups.AddToGroupAsync(Context.ConnectionId, id);
}

public class StageReporterHub : Hub
{
    public Task Watch(string id) => Groups.AddToGroupAsync(Context.ConnectionId, id);
}

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

public class StageControl
{
    private readonly Dictionary<string, AxisInfo> _axes = new();
    public void Create(string id) => _axes[id] = new AxisInfo() { Id = id };

    public AxisInfo Report(string id, double position, double velocity, double acceleration)
    {
        var info = _axes.GetValueOrDefault(id);
        if (info is null) throw new KeyNotFoundException($"Axis with id {id} not found.");
        info.Position = position;
        info.Velocity = velocity;
        info.Acceleration = acceleration;
        return info;
    }

    public AxisInfo Finished(string id, double position, double velocity, double acceleration)
    {
        var info = _axes.GetValueOrDefault(id);
        if (info is null) throw new KeyNotFoundException($"Axis with id {id} not found.");
        info.Position = position;
        info.Velocity = velocity;
        info.Acceleration = acceleration;
        _axes.Remove(id);
        return info;
    }
}

public class JobStore
{
    private readonly Dictionary<string, JobState> _jobs = new();
    public void Create(string id, int total) => _jobs[id] = new JobState { Id = id, Total = total };
    public JobState Report(string id, int current)
    {
        var job = _jobs.GetValueOrDefault(id);
        if (job is null) throw new KeyNotFoundException($"Job with id {id} not found.");

        job.Current = current;
        return job;
    }

    public JobState Complete(string id)
    {
        var job = _jobs.GetValueOrDefault(id);
        if (job is null) throw new KeyNotFoundException($"Job with id {id} not found.");
        job.Current = job.Total;
        _jobs.Remove(id);
        return job;
    }
}

public class WebApiOptions
{
    public int WorkNumber { get; set; }
}

public partial class Program { }