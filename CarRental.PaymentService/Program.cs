using CarRental.PaymentService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("BookingService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:BookingService"]!);
});

// Scoped (ikke Singleton): PaymentService afhænger af den scoped BookingServiceClient.
// Dataene overlever alligevel mellem requests, fordi Payments-dictionary'en er static.
builder.Services.AddScoped<IPaymentService, CarRental.PaymentService.Services.PaymentService>();
builder.Services.AddSingleton<IBookingServiceClient, BookingServiceClient>();

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