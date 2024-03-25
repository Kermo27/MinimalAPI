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

app.MapGet("/scan-directory", () =>
{
    try
    {
        string directoryPath = "./files";
        var baseDirLength = directoryPath.Length + 1;
        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                using var stream = File.OpenRead(path);
                using var bufferedStream = new BufferedStream(stream, 1200000);
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(bufferedStream);
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
    }
    catch (DirectoryNotFoundException)
    {
        return Results.Problem("Directory not found", statusCode: StatusCodes.Status404NotFound);
    }
    catch (Exception ex)
    {
        return Results.Problem($"An error occurred: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/download-file", async (HttpContext context, string filename) =>
{
    var filePath = Path.Combine("./files", filename);
    if (File.Exists(filePath))
    {
        context.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{Path.GetFileName(filePath)}\"");

        using var fileStream = File.OpenRead(filePath);

        await fileStream.CopyToAsync(context.Response.Body);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("File not found");
    }

});

app.MapGet("/latest-version", () =>
{
    const string filename = "version.txt";
    var filePath = Path.Combine("./files", filename);
    if (File.Exists(filePath))
    {
        var fileContent = File.ReadAllText(filePath);
        return Results.Text(fileContent);
    }
    else
    {
        return Results.Problem("Version file not found", statusCode: StatusCodes.Status404NotFound);
    }
});

app.Run();
