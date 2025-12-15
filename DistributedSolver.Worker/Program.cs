using Microsoft.AspNetCore.ResponseCompression;
using DistributedSolver.Worker.Controllers;
using System;
using System.IO;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opt => opt.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opt => opt.Level = CompressionLevel.Fastest);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.Use(async (context, next) =>
{
    var encoding = context.Request.Headers["Content-Encoding"].ToString();
    if (!string.IsNullOrWhiteSpace(encoding))
    {
        var originalBody = context.Request.Body;
        Stream? decompressed = null;
        try
        {
            if (encoding.Contains("br", StringComparison.OrdinalIgnoreCase))
            {
                decompressed = new BrotliStream(originalBody, CompressionMode.Decompress, leaveOpen: true);
            }
            else if (encoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
            {
                decompressed = new GZipStream(originalBody, CompressionMode.Decompress, leaveOpen: true);
            }

            if (decompressed != null)
            {
                context.Request.Headers["Content-Encoding"] = "identity";
                context.Request.Body = decompressed;
            }

            await next();
        }
        finally
        {
            if (decompressed != null)
            {
                await decompressed.DisposeAsync();
                context.Request.Body = originalBody;
            }
        }
    }
    else
    {
        await next();
    }
});
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Получаем порт из аргументов командной строки или переменной окружения
var port = 6000;
if (args.Length > 0 && int.TryParse(args[0], out int parsedPort))
{
    port = parsedPort;
}
else if (Environment.GetEnvironmentVariable("WORKER_PORT") != null && 
         int.TryParse(Environment.GetEnvironmentVariable("WORKER_PORT"), out int envPort))
{
    port = envPort;
}

app.Urls.Clear(); // Очищаем дефолтные URL
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine($"Воркер запущен на порту {port} (аргументы: [{string.Join(", ", args)}], WORKER_PORT: {Environment.GetEnvironmentVariable("WORKER_PORT")})");

// Автоматическая регистрация на координаторе
var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

var coordinatorUrl = Environment.GetEnvironmentVariable("COORDINATOR_URL")
    ?? (isRunningInContainer ? "http://coordinator:5000" : "http://localhost:5000");

var hostname = Environment.GetEnvironmentVariable("HOSTNAME")
    ?? (isRunningInContainer ? Environment.MachineName : "localhost");

Console.WriteLine($"Регистрация воркера: COORDINATOR_URL={coordinatorUrl}, HOSTNAME={hostname}");

var registrationLifetime = app.Lifetime;
var registrationTask = Task.Run(async () =>
{
    await Task.Delay(5000, registrationLifetime.ApplicationStopping);

    var attempt = 1;
    while (!registrationLifetime.ApplicationStopping.IsCancellationRequested)
    {
        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            var workerInfo = new
            {
                address = hostname,
                port = port
            };

            var response = await httpClient.PostAsJsonAsync(
                $"{coordinatorUrl}/api/Coordinator/register",
                workerInfo,
                registrationLifetime.ApplicationStopping);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✓ Воркер успешно зарегистрирован на координаторе: {hostname}:{port}");
                break;
            }

            var content = await response.Content.ReadAsStringAsync(registrationLifetime.ApplicationStopping);
            Console.WriteLine($"Не удалось зарегистрироваться (попытка {attempt}): {response.StatusCode} - {content}");
        }
        catch (OperationCanceledException) when (registrationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            // приложение завершается — просто выходим
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка регистрации (попытка {attempt}): {ex.Message}");
        }

        attempt++;
        var delay = Math.Min(30000, 2000 * attempt);
        await Task.Delay(delay, registrationLifetime.ApplicationStopping);
    }

    if (registrationLifetime.ApplicationStopping.IsCancellationRequested)
    {
        Console.WriteLine("Регистрация прервана — приложение завершается.");
    }
});

app.Run();

