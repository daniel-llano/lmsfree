namespace LMSFree.Infrastructure.Services;

public interface IFileStore
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);
    Stream? OpenRead(string key);
    void Delete(string key);
}

/// <summary>Keeps uploads on local disk under Storage:Path. Keys are random, so names can't collide or traverse.</summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _root;

    public LocalFileStore(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        var key = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var file = File.Create(PathFor(key));
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Stream? OpenRead(string key)
    {
        var path = PathFor(key);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string key)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
    }

    private string PathFor(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, Path.GetFileName(key)));
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid file key.");
        return path;
    }
}
