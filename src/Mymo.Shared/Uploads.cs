using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mymo.Shared;

public static class Uploads
{
    public static string GetDirectory(IConfiguration config) => Path.GetFullPath(
        config["Uploads:Directory"] ??
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "data", "uploads"));

    public static async Task<string> Save(IConfiguration config, IFormFile file)
    {
        var root = GetDirectory(config);
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

/// <summary>
/// Cloudinary upload service — dùng chung cho tất cả các microservice.
/// Register qua MymoService.Add() hoặc services.AddCloudinary(config).
/// </summary>
public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        var account = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    /// <summary>Upload ảnh hoặc video, trả về URL công khai và publicId.</summary>
    public async Task<CloudinaryUploadResult> UploadAsync(Stream stream, string fileName, string folder = "mymo")
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var isVideo = ext is ".mp4" or ".mov" or ".avi" or ".webm";

        if (isVideo)
        {
            var vp = new VideoUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
            };
            var vr = await _cloudinary.UploadAsync(vp);
            return new CloudinaryUploadResult(vr.SecureUrl?.ToString(), vr.PublicId, vr.Format, vr.Error?.Message);
        }

        var ip = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            Folder = folder,
            UseFilename = true,
            UniqueFilename = true,
        };
        var ir = await _cloudinary.UploadAsync(ip);
        return new CloudinaryUploadResult(ir.SecureUrl?.ToString(), ir.PublicId, ir.Format, ir.Error?.Message);
    }

    /// <summary>Xóa file khỏi Cloudinary theo publicId.</summary>
    public async Task<bool> DeleteAsync(string publicId, bool isVideo = false)
    {
        var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = isVideo ? ResourceType.Video : ResourceType.Image
        });
        return result.Result == "ok";
    }
}

public record CloudinaryUploadResult(string? Url, string? PublicId, string? Format, string? Error)
{
    public bool Success => Error == null && Url != null;
}

public static class CloudinaryExtensions
{
    public static IServiceCollection AddCloudinary(this IServiceCollection services)
        => services.AddSingleton<CloudinaryService>();
}
