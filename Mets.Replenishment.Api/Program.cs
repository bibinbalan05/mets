using Microsoft.EntityFrameworkCore;
using Mets.Replenishment.Infrastructure.Data;
using Mets.Replenishment.Infrastructure.Services;
using Mets.Replenishment.Core.Interfaces;
using Mets.Replenishment.Api.Background;
using System.Text.Json.Serialization;
using FluentValidation;
using Mets.Replenishment.Core.Validators;
using Mets.Replenishment.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ReplenishmentDbContext>(options =>
    options.UseInMemoryDatabase("MetsReplenishmentDb"));

builder.Services.AddScoped<IStockValidationService, MockStockValidationService>();
builder.Services.AddSingleton<IValidationJobQueue, ValidationJobQueue>();
builder.Services.AddScoped<IReplenishmentService, ReplenishmentService>();
builder.Services.AddHostedService<StockValidationBackgroundService>();

builder.Services.AddValidatorsFromAssemblyContaining<ReplenishmentRequestValidator>();

// CORS for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Seed Data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ReplenishmentDbContext>();
    DbSeeder.Seed(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
