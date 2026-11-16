using CarRental.PaymentService.Data;
using CarRental.PaymentService.Messaging;
using CarRental.PaymentService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PostgreSQL via EF Core. Connection string kommer fra "ConnectionStrings:Default".
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npgsql => npgsql.EnableRetryOnFailure()));

// Scoped: én DbContext pr. request.
builder.Services.AddScoped<IPaymentService, CarRental.PaymentService.Services.PaymentService>();

// RabbitMQ: betalingsresultater sendes som events (payment.succeeded / payment.failed).
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IPaymentEventPublisher, RabbitMqPaymentEventPublisher>();

var app = builder.Build();

// Opret tabeller og indsæt testdata (se Data/DatabaseInitializer.cs).
await app.InitializeDatabaseAsync<PaymentDbContext>(PaymentSeedData.SeedAsync);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();