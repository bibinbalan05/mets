using Microsoft.EntityFrameworkCore;
using Mets.Replenishment.Infrastructure.Data;
using Mets.Replenishment.Infrastructure.Services;
using Mets.Replenishment.Core.Interfaces;
using Mets.Replenishment.Api.Background;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => 
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ReplenishmentDbContext>(options =>
    options.UseInMemoryDatabase("MetsReplenishmentDb"));

builder.Services.AddScoped<IStockValidationService, MockStockValidationService>();
builder.Services.AddSingleton<ValidationJobQueue>();
builder.Services.AddHostedService<StockValidationBackgroundService>();

// CORS for Blazor WASM
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
