using CarRental.PaymentService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("BookingService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:BookingService"]!);
});

builder.Services.AddSingleton<IPaymentService, CarRental.PaymentService.Services.PaymentService>();
builder.Services.AddScoped<IBookingServiceClient, BookingServiceClient>();

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
