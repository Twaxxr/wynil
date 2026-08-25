using System.Security.Cryptography;

namespace NowSpinning.Media;

public sealed class ArtworkCache
{
    private readonly string _directory;

    public ArtworkCache(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NowSpinning", "Artwork");
        Directory.CreateDirectory(_directory);
    }

    public async Task<string?> StoreAsync(Stream source, string identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);

        await using var buffered = new MemoryStream();
        await source.CopyToAsync(buffered, cancellationToken).ConfigureAwait(false);
        if (buffered.Length is 0 or > 10_485_760) return null;
        var identityHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..24];
        var contentHash = Convert.ToHexString(SHA256.HashData(buffered.GetBuffer().AsSpan(0, checked((int)buffered.Length)))).ToLowerInvariant();
        var path = Path.Combine(_directory, $"{identityHash}-{contentHash}.img");
        if (File.Exists(path))
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return path;
        }
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        buffered.Position = 0;
        await using (var target = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await buffered.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        try { File.Move(temporaryPath, path, false); }
        catch (IOException) when (File.Exists(path)) { File.Delete(temporaryPath); }
        return path;
    }

    public void Trim(long maximumBytes, TimeSpan maximumAge)
    {
        var files = new DirectoryInfo(_directory).EnumerateFiles("*.img")
            .OrderByDescending(file => file.LastWriteTimeUtc).ToArray();
        long retained = 0;
        foreach (var file in files)
        {
            retained += file.Length;
            if (retained > maximumBytes || DateTime.UtcNow - file.LastWriteTimeUtc > maximumAge)
            {
                file.Delete();
            }
        }
    }

    public int Clear()
    {
        var removed = 0;
        foreach (var file in new DirectoryInfo(_directory).EnumerateFiles())
        {
            try { file.Delete(); removed++; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return removed;
    }
}
