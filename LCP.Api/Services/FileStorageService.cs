namespace LCP.Api.Services;

public interface IFileStorageService
{
    Task<string> GuardarArchivoAsync(Stream stream, string nombreArchivo, string subcarpeta = "documentos");
}

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public FileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> GuardarArchivoAsync(Stream stream, string nombreArchivo, string subcarpeta = "documentos")
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads", subcarpeta);

        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var ext = Path.GetExtension(nombreArchivo);
        var nombreUnico = $"{Guid.NewGuid():N}{ext}";
        var rutaCompleta = Path.Combine(uploadsFolder, nombreUnico);

        using (var fileStream = new FileStream(rutaCompleta, FileMode.Create))
        {
            await stream.CopyToAsync(fileStream);
        }

        return $"/uploads/{subcarpeta}/{nombreUnico}";
    }
}
