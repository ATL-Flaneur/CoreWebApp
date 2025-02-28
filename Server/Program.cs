using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Prometheus;
using Microsoft.AspNetCore.Mvc;

// Need $ dotnet add package prometheus-net.AspNetCore

// Instantiate a web application builder object.
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// TODO: OpenAPI docs (https://aka.ms/aspnet/openapi)
builder.Services.AddOpenApi();

// Build the web application.
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Set JSON serializer options.
JsonSerializerOptions options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

// Define custom Prometheus metrics.
var successfulUserAdds = Metrics.CreateCounter("successful_user_adds", "Number of successful user adds.");
var failedUserAdds = Metrics.CreateCounter("failed_user_adds", "Number of failed user adds.");
var successfulUserDeletes = Metrics.CreateCounter("successful_user_deletes", "Number of successful user deletes.");
var failedUserDeletes = Metrics.CreateCounter("failed_user_deletes", "Number of failed user deletes.");
var numActiveUsers = Metrics.CreateGauge("num_active_users", "Number of users.");

// Redirect HTTP to HTTPS.
app.UseHttpsRedirection();

// List of users added by POST commands.
List<User> userList = new();

// Add Prometheus middleware and metrics.
app.UseMetricServer();
app.UseRouting();
app.UseHttpMetrics();

// Return system health. If the app is running, it's healthy.
// TODO: migrate to app.MapHealthChecks()
app.MapGet("/health", async context =>
{
    context.Response.StatusCode = 200;
    context.Response.ContentType = "application/json";
    var data = new { Message = "OK" };
    await JsonSerializer.SerializeAsync(context.Response.Body, data, options);
});

// GET command for system stats.
app.MapGet("/stats", async context =>
{
    var cpuUsage = System.Environment.CpuUsage;
    var processorCount = System.Environment.ProcessorCount;
    var osVersion = System.Environment.OSVersion;
    var numUsers = userList.Count;

    var data = new { CpuUsage = cpuUsage, ProcessorCount = processorCount, OSVersion = osVersion, NumUsers = numUsers };

    await JsonSerializer.SerializeAsync(context.Response.Body, data, options);
});

// GET command for getting a list of users.
app.MapGet("/getusers", async context =>
{
    var users = userList.ToArray();
    await JsonSerializer.SerializeAsync(context.Response.Body, users, options);
});

// POST command for adding a user.
app.MapPost("/adduser", (User user, HttpContext context) =>
{
    var validationContext = new ValidationContext(user, serviceProvider: null, items: null);
    var validationResults = new List<ValidationResult>();
    bool isValid = Validator.TryValidateObject(user, validationContext, validationResults, true);

    if (!isValid)
    {
        failedUserAdds.Inc();
        // Return failure.
        return Results.BadRequest(validationResults);
    }

    // Valid user, so add to list of users.
    userList.Add(user);
    successfulUserAdds.Inc();
    numActiveUsers.Set(userList.Count);

    // Confirm that everything's okay.
    string result = $"Received user: FirstName={user.LastName}, LastName={user.FirstName}, Age={user.Age}";
    return Results.Ok(result);
});

// POST command for deleting a user.
app.MapPost("/deluser", ([FromBody] DeleteUserRequest request, HttpContext context) =>
{
    string result;
    User? userToRemove = userList.Find(user => user.Id == request.UserId);
    if (userToRemove == null)
    {
        result = $"No user found with ID {request.UserId}";
        failedUserDeletes.Inc();
        // Return failure.
        return Results.NotFound(result);
    }
    result = $"Removed user: FirstName={userToRemove.LastName}, LastName={userToRemove.FirstName}, Age={userToRemove.Age}";
    userList.Remove(userToRemove);
    successfulUserDeletes.Inc();
    numActiveUsers.Set(userList.Count);

    // Return success.
    return Results.Ok(result);
});

// Run web app.
app.Run();

// User definition and validation logic.
public class User
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "First name is 1…50 characters.")]
    public string FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Last name is 1…50 characters.")]
    public string LastName { get; set; }

    [Required(ErrorMessage = "Age is required.")]
    [Range(0, 100, ErrorMessage = "Age is 0…100")]
    public int Age { get; set; }

    [Required(ErrorMessage = "ID is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "ID is a non-negative integer.")]
    public int Id { get; set; }

    // First user is 0, then 1, etc.
    private static int nextId = 0;

    public User(String firstName, String lastName, int age)
    {
        FirstName = firstName;
        LastName = lastName;
        Age = age;
        Id = Interlocked.Increment(ref nextId) - 1; // Thread-safe increment.
    }
}

// Class for parsing user deletion requests.
public class DeleteUserRequest
{
    public int UserId { get; set; }
}

