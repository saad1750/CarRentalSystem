using CarRental.PaymentService.Messaging;
using CarRental.PaymentService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dataene overlever mellem requests, fordi Payments-dictionary'en er static.
builder.Services.AddScoped<IPaymentService, CarRental.PaymentService.Services.PaymentService>();

// RabbitMQ: betalingsresultater sendes som events (payment.succeeded / payment.failed).
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IPaymentEventPublisher, RabbitMqPaymentEventPublisher>();

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