using CarRental.BookingService.Messaging;
using CarRental.BookingService.Services;

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

// Scoped (ikke Singleton): BookingService afhænger af scoped HTTP-klienter.
// Dataene overlever alligevel mellem requests, fordi Bookings-dictionary'en er static.
builder.Services.AddScoped<IBookingService, CarRental.BookingService.Services.BookingService>();
builder.Services.AddSingleton<ICarServiceClient, CarServiceClient>();
builder.Services.AddSingleton<IUserServiceClient, UserServiceClient>();

// RabbitMQ: asynkron kommunikation med Payment og Notification Service.
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();   // booking.confirmed -> Notification
builder.Services.AddHostedService<PaymentEventsConsumer>();                  // payment.* <- Payment

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();