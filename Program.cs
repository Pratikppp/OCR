using Amazon.Textract;
using HealthCardApi.Services;
using HealthCardApi.Mappers;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// CORS configuration - FIXED
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonTextract>();
builder.Services.AddSingleton<FastTextractService>(); 

var app = builder.Build();

// Use CORS - MUST be called before other middleware
app.UseCors("AllowAll");

// Add port configuration for production
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

app.MapPost("/extract", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var file = form.Files["file"];
    
    if (file == null || file.Length == 0)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("No file uploaded.");
        return;
    }

    try
    {
        var fastTextractService = context.RequestServices.GetRequiredService<FastTextractService>();
        var result = await fastTextractService.AnalyzeDocumentSync(file);

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsync($$"""
        {
            "rawText": "{{System.Text.Json.JsonEncodedText.Encode(result.RawText)}}",
            "structuredData": {
                "holder_name": "{{result.StructuredData["holder_name"]}}",
                "holder_address": "{{result.StructuredData["holder_address"]}}",
                "holder_postal_city": "{{result.StructuredData["holder_postal_city"]}}",
                "cpr": "{{result.StructuredData["cpr"]}}",
                "doctor_name": "{{result.StructuredData["doctor_name"]}}",
                "doctor_address": "{{result.StructuredData["doctor_address"]}}",
                "doctor_phone": "{{result.StructuredData["doctor_phone"]}}",
                "municipality": "{{result.StructuredData["municipality"]}}",
                "region": "{{result.StructuredData["region"]}}",
                "valid_from": "{{result.StructuredData["valid_from"]}}",
                "date_of_birth": "{{result.StructuredData["date_of_birth"]}}",
                "age": "{{result.StructuredData["age"]}}",
                "gender": "{{result.StructuredData["gender"]}}"
            },
            "message": "Processing completed successfully.",
            "processingTime": "fast_sync"
        }
        """);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error processing document: {ex.Message}");
    }
});

app.Run();