using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Prometheus;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

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
app.MapGet("/health", (HttpContext context) =>
{
    return Results.Ok("OK");
});

// GET command for system stats.
app.MapGet("/stats", (HttpContext context) =>
{
    var cpuUsage = System.Environment.CpuUsage;
    var processorCount = System.Environment.ProcessorCount;
    var osVersion = System.Environment.OSVersion;
    var numUsers = userList.Count;

    return Results.Ok(new
    {
        CpuUsage = cpuUsage,
        ProcessorCount = processorCount,
        OSVersion = osVersion,
        NumUsers = numUsers
    });
});

// GET command for getting a list of users.
app.MapGet("/getusers", (HttpContext context) =>
{
    var users = userList.ToArray();
    return Results.Ok(users);
});

// POST command for adding a user.
app.MapPost("/adduser", ([FromBody] AddUserRequest request, HttpContext context) =>
{
    // Check that the incoming request is valid.
    if (!IsValid(request, out var validationResult))
    {
        failedUserAdds.Inc();
        return Results.ValidationProblem(ValidationErrors(validationResult));
    }

    // Valid user, so add to list of users.
    User user = new User(request);
    userList.Add(user);
    successfulUserAdds.Inc();
    numActiveUsers.Set(userList.Count);

    // Confirm success and return the user ID.
    AddUserResponse response = new AddUserResponse { Id = user.Id };
    return Results.Ok(response);
});

// POST command for deleting a user.
app.MapDelete("/users/{id}", (int id) =>
{
    var userToRemove = userList.Find(user => user.Id == id);
    if (userToRemove == null)
    {
        failedUserDeletes.Inc();
        return Results.NotFound(new { Message = $"User ID {id} not found" });
    }
    userList.Remove(userToRemove);
    successfulUserDeletes.Inc();
    numActiveUsers.Set(userList.Count);
    return Results.NoContent();
});

// POST command for clearing the user list.
app.MapPost("/clearusers", (ClearAllUsers request, HttpContext context) =>
{
    // Check that the incoming request is valid.
    if (!IsValid(request, out var validationResult))
    {
        failedUserClears.Inc();
        return Results.ValidationProblem(ValidationErrors(validationResult));
    }

    // Check if the specified number of users is correct.
    if (request.NumUsers != userList.Count)
    {
        failedUserClears.Inc();
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            { "NumUsers", [$"Expected {userList.Count} users, got {request.NumUsers}"] }
        });
    }
    // Number specified is correct. Remove all users.
    userList.Clear();
    numActiveUsers.Set(userList.Count);
    successfulUserClears.Inc();

    // Return "204 No Content" indicating success.
    return Results.NoContent();
});

// Run web app.
app.Run();

// Helper method for validating incoming JSON requests.
static bool IsValid<T>(T obj, out ICollection<ValidationResult> results) where T : class
{
    var validationContext = new ValidationContext(obj);
    results = new List<ValidationResult>();

    return Validator.TryValidateObject(obj, validationContext, results, true);
}

static IDictionary<string, string[]> ValidationErrors(ICollection<ValidationResult> results)
{
    var errors = results
        .GroupBy(r => r.MemberNames.FirstOrDefault() ?? "General")
        .ToDictionary(
            g => g.Key,
            g => g.Select(r => r.ErrorMessage ?? string.Empty) // Map nulls to empty strings.
                 .ToArray()
        );
    return errors;
}


// User definition and validation logic.
public class AddUserRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "First name is 1…50 characters.")]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Last name is 1…50 characters.")]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Age is required.")]
    [Range(0, 100, ErrorMessage = "Age is 0…100")]
    public int? Age { get; set; }
}

// Class to hold a user.
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public int Id { get; set; }

    // First user is 0, then 1, etc.
    private static int nextId = 0;

    public User(AddUserRequest addUserRequest)
    {
        // Values are non-null due to [Required].
        FirstName = addUserRequest.FirstName!;
        LastName = addUserRequest.LastName!;
        Age = addUserRequest.Age!.Value;
        Id = Interlocked.Increment(ref nextId) - 1; // Thread-safe increment (belt & suspenders).
    }

    public User(String firstName, String lastName, int age)
    {
        FirstName = firstName;
        LastName = lastName;
        Age = age;
        Id = Interlocked.Increment(ref nextId) - 1; // Thread-safe increment (belt & suspenders).
    }
}

// Class for returning the ID assigned to a new user.
public class AddUserResponse
{
    public int Id { get; set; }
}

// Class for parsing user list clear requests.
public class ClearAllUsers
{
    [Required(ErrorMessage = "NumUsers is required")]
    [Range(0, int.MaxValue, ErrorMessage = "NumUsers is a non-negative integer.")]
    public int? NumUsers { get; set; }
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
    public User? Find(Predicate<User> predicate)
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