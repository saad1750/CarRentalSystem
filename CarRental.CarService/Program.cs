using CarRental.CarService.Data;
using CarRental.CarService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PostgreSQL via EF Core. Connection string kommer fra "ConnectionStrings:Default".
builder.Services.AddDbContext<CarDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.EnableRetryOnFailure()));

// Scoped: én DbContext pr. request.
builder.Services.AddScoped<ICarService, CarService>();

var app = builder.Build();

// Opret tabeller og indsæt testdata (se Data/DatabaseInitializer.cs).
await app.InitializeDatabaseAsync<CarDbContext>(CarSeedData.SeedAsync);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
