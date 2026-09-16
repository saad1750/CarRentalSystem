using CarRental.UserService.Services;

var builder = WebApplication.CreateBuilder(args);

// Registrerer controllers (AuthController, UserController).
builder.Services.AddControllers();

// Lader Swagger læse endpoint-info fra controllerne.
builder.Services.AddEndpointsApiExplorer();

// Genererer OpenAPI/Swagger-dokumentationen.
builder.Services.AddSwaggerGen();

// DI: giv den in-memory implementering når IUserService bliver bedt om.
builder.Services.AddSingleton<IUserService, UserService>();

var app = builder.Build();

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
