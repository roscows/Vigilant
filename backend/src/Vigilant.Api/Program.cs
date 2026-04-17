using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using Vigilant.Api.Hubs;
using Vigilant.Api.Services;
using Vigilant.Application.Common.Alerts;
using Vigilant.Application;
using Vigilant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<IAlertPublisher, SignalRAlertPublisher>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173"];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Vigilant AML API",
        Version = "v1",
        Description = "Graph-native AML detection APIs backed by Neo4j."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Vigilant AML API v1");
        options.DocumentTitle = "Vigilant AML Swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthorization();

app.MapControllers();
app.MapHub<AlertsHub>("/hubs/alerts");

app.Run();

