namespace HRandomPlus.Core;

/// <summary>Creates a new file without overwriting another writer's output.</summary>
public static class UniqueFile
{
    public static string Write(string candidate, Action<FileStream> write)
    {
        FileStream stream = Reserve(candidate);
        string path = stream.Name;
        // Cleanup is reachable only after this operation successfully created the file.
        try
        {
            using (stream) write(stream);
            return path;
        }
        catch
        {
            try { File.Delete(path); } catch { }
            throw;
        }
    }

    /// <summary>The caller owns the returned file and must dispose its handle.</summary>
    public static FileStream Reserve(string candidate)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(candidate))!;
        string name = Path.GetFileNameWithoutExtension(candidate);
        string extension = Path.GetExtension(candidate);
        for (int index = 1; ; index++)
        {
            string path = Path.Combine(directory, index == 1
                ? Path.GetFileName(candidate) : $"{name} {index}{extension}");
            try
            {
                return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (File.Exists(path) || Directory.Exists(path))
            {
                continue;
            }
        }
    }
}
