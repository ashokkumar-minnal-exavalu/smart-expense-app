using Azure;

using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Register Azure Clients
builder.Services.AddAzureClients(clientBuilder =>
{
    // Configure Blob Storage
    clientBuilder.AddBlobServiceClient(builder.Configuration["BlobStorageConnectionString"]);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoints

app.MapPost("/expenses/upload", async (
    IFormFile file, 
    [FromServices] BlobServiceClient blobServiceClient, 
    ILogger<Program> logger) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("No file uploaded.");
    }

    var expenseId = Guid.NewGuid().ToString();
    var blobName = $"{expenseId}-{file.FileName}";
    
    // 1. Upload to Blob Storage
    var containerClient = blobServiceClient.GetBlobContainerClient("expenses-inbox");
    // Ensure the container exists
    await containerClient.CreateIfNotExistsAsync();
    
    var blobClient = containerClient.GetBlobClient(blobName);
    
    using (var stream = file.OpenReadStream())
    {
        await blobClient.UploadAsync(stream, overwrite: true);
    }
    
    logger.LogInformation("File uploaded to blob storage with ID: {ExpenseId}. Blob created event will trigger downstream processing.", expenseId);

    return Results.Accepted($"/expenses/{expenseId}", new { Id = expenseId, Status = "Uploaded" });
})
.WithName("UploadExpense")
.DisableAntiforgery(); // Disable anti-forgery for this API endpoint since it accepts files

app.MapGet("/expenses/{id}", (string id, ILogger<Program> logger) =>
{
    // Mocked GET response
    logger.LogInformation("Retrieving status for expense ID: {ExpenseId}", id);
    
    var status = new
    {
        Id = id,
        Status = "Processing", // Mocked status
        LastUpdated = DateTime.UtcNow
    };

    return Results.Ok(status);
})
.WithName("GetExpenseStatus");

app.Run();
