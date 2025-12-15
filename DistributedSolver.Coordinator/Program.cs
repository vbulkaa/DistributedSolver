using Microsoft.AspNetCore.ResponseCompression;
using System;
using System.IO;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200 MB, enough for large matrices
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();
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

var port = args.Length > 0 && int.TryParse(args[0], out int parsedPort) ? parsedPort : 5000;
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine($"Координатор запущен на порту {port}");
Console.WriteLine($"Swagger UI доступен по адресу: http://localhost:{port}/swagger");

app.Run();

