using ImageMetadataParser.Components;
using ImageMetadataParser.Data;
using ImageMetadataParser.Services;
using ImageMetadataParser.Services.Parsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddHttpClient(); 

// Add these for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Image Metadata Parser API",
        Version = "v1",
        Description = "Extract metadata from image files including EXIF, XMP, and AI analysis"
    });
    // Enable file upload support
    c.OperationFilter<FileUploadOperationFilter>();
});

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // For development, use SQL Server
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null
        )
    );
});

builder.Services.AddScoped<ImageUploadService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<KeywordService>();
builder.Services.AddScoped<AiImageAnalyzer>();
builder.Services.AddScoped<IImageParser, ExifParser>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
    // Add Swagger middleware for development
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Image Metadata Parser API v1");
        c.RoutePrefix = "api-docs";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Add this BEFORE MapRazorComponents
app.MapControllers(); // This was missing - needed for API controllers

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();