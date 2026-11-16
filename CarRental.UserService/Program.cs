using CarRental.UserService.Data;
using CarRental.UserService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registrerer controllers (AuthController, UserController).
builder.Services.AddControllers();

// Lader Swagger læse endpoint-info fra controllerne.
builder.Services.AddEndpointsApiExplorer();

// Genererer OpenAPI/Swagger-dokumentationen.
builder.Services.AddSwaggerGen();

// PostgreSQL via EF Core. Connection string kommer fra "ConnectionStrings:Default"
// (appsettings.json lokalt, miljøvariablen ConnectionStrings__Default i Docker).
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.EnableRetryOnFailure()));

// Scoped: én DbContext pr. request.
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

// Opret tabeller og indsæt testdata (se Data/DatabaseInitializer.cs).
await app.InitializeDatabaseAsync<UserDbContext>(UserSeedData.SeedAsync);

// Swagger UI vises kun i Development.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Auth-pipeline (endnu ikke koblet til rigtig login — se TODO i AuthController).
app.UseAuthorization();

app.MapControllers();
app.Run();
