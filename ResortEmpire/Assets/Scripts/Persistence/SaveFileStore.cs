using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Reads and writes the save file on the device. No Unity or network dependency.
// Writing goes to a temporary file first; the previous save is kept as a backup, so an
// interrupted write or a damaged file never loses more than the latest save.
public sealed class SaveFileStore
{
    public string MainPath { get; }
    public string BackupPath { get; }
    public string TempPath { get; }
    public string CorruptPath { get; }

    public SaveFileStore(string directory, string fileName)
    {
        if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
        if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("Save file name is missing.", nameof(fileName));

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        MainPath = Path.Combine(directory, fileName);
        BackupPath = Path.Combine(directory, name + ".backup" + extension);
        TempPath = Path.Combine(directory, name + ".tmp");
        CorruptPath = Path.Combine(directory, name + ".corrupt" + extension);
    }

    public bool AnyFileExists => File.Exists(MainPath) || File.Exists(BackupPath);

    public void Write(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        Directory.CreateDirectory(Path.GetDirectoryName(MainPath));

        // 1. The new save is fully written and flushed before anything else is touched.
        using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        // 2. The current save becomes the backup, 3. the new one takes its place.
        if (File.Exists(MainPath))
        {
            File.Copy(MainPath, BackupPath, true);
            File.Delete(MainPath);
        }
        File.Move(TempPath, MainPath);
    }

    // The main save first, then the backup. Files that cannot be read are skipped.
    public IEnumerable<(string path, string json)> ReadCandidates()
    {
        foreach (string path in new[] { MainPath, BackupPath })
        {
            string json = null;
            try
            {
                if (File.Exists(path)) json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            if (json != null) yield return (path, json);
        }
    }

    // Moves a damaged file aside so it is never copied over the good backup, but stays
    // available for inspection.
    public void Quarantine(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            if (File.Exists(CorruptPath)) File.Delete(CorruptPath);
            File.Move(path, CorruptPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public void DeleteAll()
    {
        foreach (string path in new[] { MainPath, BackupPath, TempPath })
            if (File.Exists(path)) File.Delete(path);
    }
}
