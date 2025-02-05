using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Prometheus;

// Need $ dotnet add package prometheus-net.AspNetCore

/* Add users with:
curl -X POST \
-H "Content-Type: application/json" \
-d '{"firstName":"John", "lastName":"Smith", "Age":42}' \ 
http://localhost:5015/adduser */

// Instantiate a web application builder object.
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// TODO: OpenAPI docs (https://aka.ms/aspnet/openapi)
builder.Services.AddOpenApi();

// Build the web application.
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

// Set JSON serializer options.
JsonSerializerOptions options = new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

// Define custom Prometheus metrics.
var failedUserAdds = Metrics.CreateCounter("failed_user_adds", "Number of failed user adds.");
var successfulUserAdds = Metrics.CreateCounter("successful_user_adds", "Number of successful user adds.");

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
app.MapGet("/health", async context => {
    context.Response.StatusCode = 200;
    context.Response.ContentType = "application/json";
    var data = new { Message = "OK" };
    await JsonSerializer.SerializeAsync(context.Response.Body, data, options);
});

// GET command for system stats.
app.MapGet("/stats", async context => {
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
    var users =  userList.ToArray();
    await JsonSerializer.SerializeAsync(context.Response.Body, users, options);
});

// POST command for adding a user.
app.MapPost("/adduser", (User user, HttpContext context) => {
    var validationContext = new ValidationContext(user, serviceProvider: null, items: null);
    var validationResults = new List<ValidationResult>();
    bool isValid = Validator.TryValidateObject(user, validationContext , validationResults, true);

    if (!isValid) {
        failedUserAdds.Inc();
        return Results.BadRequest(validationResults);
    }

    // Valid user, so add to list of users.
    userList.Add(user);
    successfulUserAdds.Inc();

    // Confirm that everything's okay.
    string result = "Received user: FirstName={user.lastName}, LastName={user.firstName}, Age={user.Age}";
    return Results.Ok(result);
});

// Run web app.
app.Run();

// User definition and validation logic.
public class User(String firstName, String lastName, int age) {
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "First name is 1…50 characters.")]
    public string firstName { get; set; } = firstName;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Last name is 1…50 characters.")]
    public string lastName { get; set; } = lastName;

    [Required(ErrorMessage = "Age is required.")]
    [Range(0, 100, ErrorMessage = "Age is 0…100")]
    public int age { get; set; } = age;
}
