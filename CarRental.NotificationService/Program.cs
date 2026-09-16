using CarRental.NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Ingen database registreret — krav siger ingen egen database.
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddSingleton<INotificationSender, EmailNotificationSender>();
builder.Services.AddSingleton<INotificationSender, SmsNotificationSender>();

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
