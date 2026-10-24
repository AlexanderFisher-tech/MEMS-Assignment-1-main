/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Validates and stores administrator-uploaded candidate and municipality images.
 */
using SkiaSharp;

namespace Mems.Web.Services;

/// <summary>Keeps decoded, re-encoded uploads outside the public web root under generated filenames.</summary>
public class ImageStore
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public string Root { get; }
    public ImageStore(IConfiguration config, IWebHostEnvironment environment)
    {
        Root = Path.Combine(config["Storage:Root"] ?? Path.Combine(environment.ContentRootPath, "App_Data"), "uploads");
        Directory.CreateDirectory(Root);
    }
    /// <summary>Validates JPEG/PNG content and dimensions, then returns a generated PNG filename.</summary>
    public async Task<string> Save(IFormFile upload)
    {
        if (upload.Length is <= 0 or > MaxBytes) throw new InvalidDataException("Choose an image between 1 byte and 2 MB.");
        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png")) throw new InvalidDataException("Only JPEG and PNG images are accepted.");
        using var input = new MemoryStream();
        await upload.CopyToAsync(input);
        if (input.Length > MaxBytes) throw new InvalidDataException("Choose an image no larger than 2 MB.");
        using var data = SKData.CreateCopy(input.ToArray());
        using var codec = SKCodec.Create(data);
        if (codec is null) throw new InvalidDataException("The file is not a valid image.");
        // Inspect dimensions before allocating the decoded pixel buffer to bound decompression memory.
        var info = codec.Info;
        if (info.Width > 6000 || info.Height > 6000 || (long)info.Width * info.Height > 16000000)
            throw new InvalidDataException("Image dimensions must be at most 6000 pixels and 16 megapixels.");
        if ((extension == ".png" && codec.EncodedFormat != SKEncodedImageFormat.Png) ||
            (extension != ".png" && codec.EncodedFormat != SKEncodedImageFormat.Jpeg))
            throw new InvalidDataException("The file contents must match the JPEG or PNG extension.");
        if (codec.FrameCount > 1) throw new InvalidDataException("Animated images are not accepted.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("The image is damaged or invalid.");
        // Re-encoding resized pixels omits original file metadata and never uses the client filename as a path.
        var ratio = Math.Min(1d, 1200d / Math.Max(info.Width, info.Height));
        using var resized = bitmap.Resize(new SKImageInfo(Math.Max(1, (int)(info.Width * ratio)), Math.Max(1, (int)(info.Height * ratio))), new SKSamplingOptions(SKFilterMode.Linear));
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var name = Guid.NewGuid().ToString("N") + ".png";
        await File.WriteAllBytesAsync(Path.Combine(Root, name), encoded.ToArray());
        return name;
    }
    /// <summary>Deletes only generated upload names; bundled demo portraits and arbitrary paths are excluded.</summary>
    public void Delete(string? name)
    {
        if (name is not null && System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-f0-9]{32}\\.png$"))
            File.Delete(Path.Combine(Root, name));
    }
}
