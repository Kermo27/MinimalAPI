using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/scan-directory", (string directoryPath) =>
{
    var baseDirLength = directoryPath.Length + 1;
    var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
        .Select(path =>
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(File.ReadAllBytes(path));
            var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            var fileInfo = new FileInfo(path);
            return new
            {
                Path = path.Substring(baseDirLength),
                Name = Path.GetFileName(path),
                Hash = hash,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc.ToString("o")
            };
        }).ToList();

    return Results.Json(files);
});

app.MapGet("/download-file", async (HttpContext context, string filename) =>
{
    var filePath = Path.Combine("./files", filename);
    if (File.Exists(filePath))
    {
        var fileContent = File.OpenRead(filePath);
        context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(filePath)}\"");
        await context.Response.BodyWriter.WriteAsync(new ReadOnlyMemory<byte>(await File.ReadAllBytesAsync(filePath)));
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("File not found");
    }
});

app.Run();
