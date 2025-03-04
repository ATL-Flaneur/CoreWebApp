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
var successfulUserClears = Metrics.CreateCounter("successful_user_clears", "Number of successful user clears.");
var failedUserClears = Metrics.CreateCounter("failed_user_clears", "Number of failed user clears.");
var numActiveUsers = Metrics.CreateGauge("num_active_users", "Number of users.");
var totalMemoryGauge = Metrics.CreateGauge("system_memory_total_bytes", "Total memory available to the runtime in bytes.");

// Abstract view of memory available in the container. Getting physical memory would require OS-level calls that differ in Linux, MacOS, and Windows.
totalMemoryGauge.Set(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);

// Redirect HTTP to HTTPS.
app.UseHttpsRedirection();

// List of users added by POST commands.
UserList userList = new UserList();

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

    // Check if the specified user exists.
    User? userToRemove = userList.Find(user => user.Id == request.UserId);
    if (userToRemove == null)
    {
        result = $"No user found with ID {request.UserId}";
        failedUserDeletes.Inc();
        // Return failure.
        return Results.NotFound(result);
    }
    // User found. Delete and report success.
    result = $"Removed user: FirstName={userToRemove.LastName}, LastName={userToRemove.FirstName}, Age={userToRemove.Age}";
    userList.Remove(userToRemove);


    numActiveUsers.Set(userList.Count);

    // Return success.
    return Results.Ok(result);
});

// POST command for clearing the user list.
app.MapPost("/clearusers", ([FromBody] ClearAllUsers request, HttpContext context) =>
{
    string result;
    // Check if the specified number of users is correct.
    if (request.NumUsers != userList.Count)
    {
        result = $"Incorrect user count.";
        failedUserClears.Inc();
        // Return failure.
        return Results.BadRequest(result);
    }
    // Number specified is correct. Remove all users.
    result = $"Removed {request.NumUsers} users.";
    userList.Clear();
    successfulUserClears.Inc();
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
        Id = Interlocked.Increment(ref nextId) - 1; // Thread-safe increment (belt & suspenders).
    }
}

// Thread-safe user list.
public class UserList
{
    private List<User> _list = new List<User>();
    private readonly object _lock = new object();

    // Add an user to the list.
    public void Add(User item)
    {
        lock (_lock)
        {
            _list.Add(item);
        }
    }

    // Add a user to the list.
    public void Remove(User userToRemove)
    {
        lock (_lock)
        {
            _list.Remove(userToRemove);
        }
    }

    // Find an item based on a predicate. May return null.
    public User Find(Predicate<User> predicate)
    {
        lock (_lock)
        {
            return _list.Find(predicate);
        }
    }

    // Get or set an element by index.
    public User this[int index]
    {
        get
        {
            lock (_lock)
            {
                if (index < 0 || index >= _list.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _list[index];
            }
        }
        set
        {
            lock (_lock)
            {
                if (index < 0 || index >= _list.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                _list[index] = value;
            }
        }
    }

    // Get the number of elements in the list.
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _list.Count;
            }
        }
    }

    // Delete all items from the list.
    public void Clear()
    {
        {
            lock (_lock)
            {
                _list.Clear();
            }
        }
    }

    // Return the list as an array.
    public User[] ToArray()
    {
        lock (_lock)
        {
            return _list.ToArray();
        }
    }
}

// Class for parsing user deletion requests.
public class DeleteUserRequest
{
    public int UserId { get; set; }
}

// Class for parsing user list clear requests.
public class ClearAllUsers
{
    public int NumUsers { get; set; }
}