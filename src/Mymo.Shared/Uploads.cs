using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Mymo.Shared;

public static class Uploads
{
    public static async Task<string> Save(IConfiguration config, IFormFile file)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "uploads"));
        Directory.CreateDirectory(root);
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(root, name);
        await using var stream = File.Create(path);
        await file.CopyToAsync(stream);
        var origin = config["PublicOrigin"] ?? "http://localhost:5079";
        return $"{origin}/uploads/{name}";
    }
}
