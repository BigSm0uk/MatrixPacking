using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<MatrixPackingService>();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(x =>
        x.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
    );
}
else
    app.UseCors(x =>
        x.WithOrigins("https://mafinity.ru")
            .AllowAnyMethod()
            .AllowAnyHeader()
    );
app.MapHealthChecks("/health");

app.UseAuthorization();

app.MapControllers();

app.Run();