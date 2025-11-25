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
builder.Services.AddSingleton<PdfToImageService>();
builder.Services.AddSingleton<PdfTextExtractionService>();

var app = builder.Build();

// Use CORS - MUST be called before other middleware
app.UseCors("AllowAll");

// Add port configuration for production
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// Register the new service


// Update your extract endpoint
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
        var pdfToImageService = context.RequestServices.GetRequiredService<PdfToImageService>();
        var pdfTextExtractionService = context.RequestServices.GetRequiredService<PdfTextExtractionService>();
        
        AnalysisResult result;
        
        // Check if file is PDF
        if (pdfToImageService.IsPdfFile(file))
        {
            // Use direct PDF text extraction (Option 2)
            using var pdfStream = file.OpenReadStream();
            result = await pdfTextExtractionService.ExtractFromPdf(pdfStream);
        }
        else
        {
            // Process image file directly (existing logic)
            result = await fastTextractService.AnalyzeDocumentSync(file);
        }

        // Your existing response code...
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsync($$"""
    {
    "rawText": "{{System.Text.Json.JsonEncodedText.Encode(result.RawText)}}",
    "structuredData": {
        "holder_first_name": "{{result.StructuredData["holder_first_name"]}}",
        "holder_surname": "{{result.StructuredData["holder_surname"]}}",
        "holder_name": "{{result.StructuredData["holder_name"]}}",
        "holder_address": "{{result.StructuredData["holder_address"]}}",
        "postal_code": "{{result.StructuredData["postal_code"]}}",
        "city": "{{result.StructuredData["city"]}}",
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
        "processingTime": "{{(pdfToImageService.IsPdfFile(file) ? "pdf_direct" : "fast_sync")}}"
        }
""");   
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Error processing document: {ex.Message}");
    }
});
app.MapGet("/travel-info", async (HttpContext context) =>
{
    var response = new
    {
        weather = new
        {
            conditions = "Partially cloudy",
            description = "Partly cloudy throughout the day",
            icon = "partly-cloudy-day"
        },
        delays = new
        {
            departures = "50%",
            arrivals = "30%"
        },
        safety = new
        {
            level = "HighRisk"
        },
        health = new
        {
            vaccinations = new[] 
            {
                "Routine",
                "Hepatitis A & B",
                "Typhoid",
                "Rabies",
                "Polio"
            },
            diseases = new[]
            {
                "Malaria (high risk)",
                "Cholera",
                "Tuberculosis",
                "Crimean-Congo hemorrhagic fever"
            },
            malariaRisk = "High, especially rural areas",
            precautions = "Avoid animal bites"
        }
    };
    
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
});

app.Run();