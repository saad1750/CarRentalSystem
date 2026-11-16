using CarRental.BookingService.Data;
using CarRental.BookingService.Messaging;
using CarRental.BookingService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Adresser hentes fra appsettings.json ("Services:CarService" osv.).
builder.Services.AddHttpClient("CarService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:CarService"]!);
});

builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UserService"]!);
});

// PostgreSQL via EF Core. Connection string kommer fra "ConnectionStrings:Default".
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.EnableRetryOnFailure()));

// Scoped: én DbContext pr. request (og pr. RabbitMQ-besked, se PaymentEventsConsumer).
builder.Services.AddScoped<IBookingService, CarRental.BookingService.Services.BookingService>();
builder.Services.AddSingleton<ICarServiceClient, CarServiceClient>();
builder.Services.AddSingleton<IUserServiceClient, UserServiceClient>();

// RabbitMQ: asynkron kommunikation med Payment og Notification Service.
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();   // booking.confirmed -> Notification
builder.Services.AddHostedService<PaymentEventsConsumer>();                  // payment.* <- Payment

var app = builder.Build();

// Opret tabeller og indsæt testdata (se Data/DatabaseInitializer.cs).
await app.InitializeDatabaseAsync<BookingDbContext>(BookingSeedData.SeedAsync);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();