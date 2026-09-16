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

builder.Services.AddHttpClient("NotificationService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:NotificationService"]!);
});

builder.Services.AddSingleton<IBookingService, CarRental.BookingService.Services.BookingService>();
builder.Services.AddScoped<ICarServiceClient, CarServiceClient>();
builder.Services.AddScoped<IUserServiceClient, UserServiceClient>();

// MIDLERTIDIG: kalder Notification Service over REST — skift til rigtig messaging senere.
builder.Services.AddScoped<IEventPublisher, HttpEventPublisher>();

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
