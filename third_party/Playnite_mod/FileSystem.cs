using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Playnite;

namespace PlayniteMod;

public enum FileSystemItem
{
    File,
    Directory
}

public static class FileSystem
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(FileSystem));

    public static void CreateDirectory(string path)
    {
        CreateDirectory(path, false);
    }

    public static void CreateDirectory(string path, bool clean)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            if (clean)
            {
                DeleteDirectory(path, true);
            }
            else
            {
                return;
            }
        }

        Directory.CreateDirectory(path);
    }

    public static void PrepareSaveFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!dir.IsNullOrEmpty())
        {
            CreateDirectory(dir);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static bool IsDirectoryEmpty(string path)
    {
        if (Directory.Exists(path))
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        return true;
    }

    public static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void CreateFile(string path)
    {
        PrepareSaveFile(path);
        File.Create(path).Dispose();
    }

    public static void CopyFile(string sourcePath, string targetPath, bool overwrite = true)
    {
        PrepareSaveFile(targetPath);
        File.Copy(sourcePath, targetPath, overwrite);
    }

    public static void DeleteDirectory(string path, bool includeReadonly = false)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        if (includeReadonly)
        {
            foreach (var s in Directory.GetDirectories(path))
            {
                DeleteDirectory(s, true);
            }

            foreach (var file in Directory.GetFiles(path))
            {
                var attr = File.GetAttributes(file);
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(file, attr ^ FileAttributes.ReadOnly);
                }

                File.Delete(file);
            }

            var dirAttr = File.GetAttributes(path);
            if ((dirAttr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(path, dirAttr ^ FileAttributes.ReadOnly);
            }

            Directory.Delete(path, false);
        }
        else
        {
            Directory.Delete(path, true);
        }
    }

    public static bool CanWriteToFolder(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            using var stream = File.Create(Path.Combine(folder, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ReadFileAsStringSafe(string path, int retryAttempts = 5)
    {
        IOException? ioException = null;
        for (int i = 0; i < retryAttempts; i++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException exc)
            {
                Logger.Debug($"Can't read from file, trying again. {path}");
                ioException = exc;
                Task.Delay(500).Wait();
            }
        }

        throw new IOException($"Failed to read {path}", ioException);
    }

    public static byte[] ReadFileAsBytesSafe(string path, int retryAttempts = 5)
    {
        IOException? ioException = null;
        for (int i = 0; i < retryAttempts; i++)
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (IOException exc)
            {
                Logger.Debug($"Can't read from file, trying again. {path}");
                ioException = exc;
                Task.Delay(500).Wait();
            }
        }

        throw new IOException($"Failed to read {path}", ioException);
    }

    public static Stream CreateWriteFileStreamSafe(string path, int retryAttempts = 5)
    {
        IOException? ioException = null;
        for (int i = 0; i < retryAttempts; i++)
        {
            try
            {
                return new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
            }
            catch (IOException exc)
            {
                Logger.Debug($"Can't open write file stream, trying again. {path}");
                ioException = exc;
                Task.Delay(500).Wait();
            }
        }

        throw new IOException($"Failed to read {path}", ioException);
    }

    public static Stream OpenReadFileStreamSafe(string path, int retryAttempts = 5)
    {
        IOException? ioException = null;
        for (int i = 0; i < retryAttempts; i++)
        {
            try
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read);
            }
            catch (IOException exc)
            {
                Logger.Debug($"Can't open read file stream, trying again. {path}");
                ioException = exc;
                Task.Delay(500).Wait();
            }
        }

        throw new IOException($"Failed to read {path}", ioException);
    }

    public static void WriteStringToFile(string path, string content)
    {
        PrepareSaveFile(path);
        File.WriteAllText(path, content);
    }

    public static string ReadStringFromFile(string path, Encoding? encoding = null)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, encoding ?? Encoding.Default);
        return sr.ReadToEnd();
    }

    public static void WriteStringToFileSafe(string path, string content, int retryAttempts = 5)
    {
        IOException? ioException = null;
        for (int i = 0; i < retryAttempts; i++)
        {
            try
            {
                PrepareSaveFile(path);
                File.WriteAllText(path, content);
                return;
            }
            catch (IOException exc)
            {
                Logger.Debug($"Can't write to a file, trying again. {path}");
                ioException = exc;
                Task.Delay(500).Wait();
            }
        }

        throw new IOException($"Failed to write to {path}", ioException);
    }

    public static void DeleteFileSafe(string path, int retryAttempts = 5)
    {
        if (!File.Exists(path))
        {
            return;
        }

        IOException? ioException = null;
        for (int i = 0; i < retryAttempts; i++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException exc)
            {
                Logger.Debug($"Can't detele file, trying again. {path}");
                ioException = exc;
                Task.Delay(500).Wait();
            }
            catch (UnauthorizedAccessException exc)
            {
                Logger.Error(exc, $"Can't detele file, UnauthorizedAccessException. {path}");
                return;
            }
        }

        throw new IOException($"Failed to delete {path}", ioException);
    }

    public static long GetFreeSpace(string drivePath)
    {
        var root = Path.GetPathRoot(drivePath);
        var drive = DriveInfo.GetDrives().FirstOrDefault(a => a.RootDirectory.FullName.Equals(root, StringComparison.OrdinalIgnoreCase));
        ;
        if (drive is not null)
        {
            return drive.AvailableFreeSpace;
        }

        return 0;
    }

    public static long GetFileSize(string path)
    {
        return GetFileSize(new FileInfo(path));
    }

    public static long GetFileSize(FileInfo fi)
    {
        return fi.Length;
    }

    public static void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs = true, bool overwrite = true)
    {
        var dir = new DirectoryInfo(sourceDirName);
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirName);
        }

        var dirs = dir.GetDirectories();
        if (!Directory.Exists(destDirName))
        {
            Directory.CreateDirectory(destDirName);
        }

        var files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string temppath = Path.Combine(destDirName, file.Name);
            file.CopyTo(temppath, overwrite);
        }

        if (copySubDirs)
        {
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                CopyDirectory(subdir.FullName, temppath, copySubDirs);
            }
        }
    }

    public static bool FileExistsOnAnyDrive(string filePath, [NotNullWhen(true)] out string? existringPath)
    {
        return PathExistsOnAnyDrive(filePath, path => File.Exists(path), out existringPath);
    }

    public static bool DirectoryExistsOnAnyDrive(string directoryPath, [NotNullWhen(true)] out string? existringPath)
    {
        return PathExistsOnAnyDrive(directoryPath, path => Directory.Exists(path), out existringPath);
    }

    private static bool PathExistsOnAnyDrive(
        string originalPath, Predicate<string> predicate, [NotNullWhen(true)] out string? existringPath)
    {
        existringPath = null;
        try
        {
            if (predicate(originalPath))
            {
                existringPath = originalPath;
                return true;
            }

            if (!Paths.IsFullPath(originalPath))
            {
                return false;
            }

            var availableDrives = DriveInfo.GetDrives().Where(d => d.IsReady);
            foreach (var drive in availableDrives)
            {
                var pathWithoutDrive = originalPath.Substring(drive.Name.Length);
                var newPath = Path.Combine(drive.Name, pathWithoutDrive);
                if (predicate(newPath))
                {
                    existringPath = newPath;
                    return true;
                }
            }
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            Logger.Error(ex, $"Error checking if path exists on different drive \"{originalPath}\"");
        }

        return false;
    }

    public static void ReplaceStringInFile(string path, string oldValue, string newValue, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        var fileContent = File.ReadAllText(path, encoding);
        if (fileContent.IsNullOrEmpty())
        {
            return;
        }

        File.WriteAllText(path, fileContent.Replace(oldValue, newValue, StringComparison.Ordinal), encoding);
    }

    public static string GetSHA256(Stream stream)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    public static string GetSHA256(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return GetSHA256(stream);
    }

    public static bool AreFileContentsEqual(string path1, string path2)
    {
        var info1 = new FileInfo(path1);
        var info2 = new FileInfo(path2);
        if (info1.Length != info2.Length)
        {
            return false;
        }

        var bufferSize = 4096;
        using var fs1 = new FileStream(path1, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var fs2 = new FileStream(path2, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];
        while (true)
        {
            var read1 = fs1.Read(buffer1, 0, bufferSize);
            var read2 = fs2.Read(buffer2, 0, bufferSize);
            if (read1 != read2)
            {
                return false;
            }

            if (read1 == 0)
            {
                return true;
            }

            if (!buffer1.SequenceEqual(buffer2))
            {
                return false;
            }

            Array.Clear(buffer1);
            Array.Clear(buffer2);
        }
    }
}