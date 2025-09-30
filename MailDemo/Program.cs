using MailDemo;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.AllowAnyOrigin()    // <- allow all origins
               .AllowAnyHeader()
               .AllowAnyMethod();
        });
});

// Configure SMTP settings
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("SmtpSettings"));

// Add Kafka services
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
//builder.Services.AddHostedService<EmailTrackingConsumer>();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Email Tracking API with Kafka",
        Version = "v1",
        Description = "Email tracking system with Kafka message queuing",
        Contact = new OpenApiContact
        {
            Name = "Mail Demo",
            Email = "demo@example.com"
        }
    });
});

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});
builder.Services.Configure<CampaignsConfig>(builder.Configuration.GetSection("CampaignsConfig"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Email Tracking API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAngularApp");
app.UseAuthorization();
app.MapControllers();

// Log startup information
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🚀 Email Tracking API Started Successfully!");
    logger.LogInformation("📚 Swagger UI: http://localhost:46915/swagger");
    logger.LogInformation("📧 API Base: http://localhost:46915/api/mail");
    logger.LogInformation("🖥️ Kafka UI: http://localhost:8080");
    logger.LogInformation("🎧 Kafka Consumer: Active and listening...");
});
app.UseStaticFiles();

app.Lifetime.ApplicationStopping.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application is stopping...");
});

app.Run();