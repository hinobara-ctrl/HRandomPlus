namespace HRandomPlus.Integration.Lazer;

public static class LazerBlobPath
{
    public static string GetRelativePath(string hash)
    {
        if (hash is null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A lazer blob hash must contain exactly 64 hexadecimal characters.", nameof(hash));
        string normalized = hash.ToLowerInvariant();
        return Path.Combine(normalized[0].ToString(), normalized[..2], normalized);
    }

    public static string GetFullPath(string storageRoot, string hash)
        => Path.Combine(Path.GetFullPath(storageRoot), "files", GetRelativePath(hash));
}
