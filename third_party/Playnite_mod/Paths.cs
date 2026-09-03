using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Win32.Storage.FileSystem;
using PInvokeWin32 = Windows.Win32.PInvoke;

namespace PlayniteMod
{
    public partial class Paths
    {
        private const string LongPathPrefix = @"\\?\";
        private const string LongPathUncPrefix = @"\\?\UNC\";
        private const int MaxPathLength = 32_767;
        private static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

        [GeneratedRegex(@"^([a-zA-Z]:\\|\\\\)")]
        private static partial Regex IsFullPathRegex();

        public static string FixSeparators(string path)
        {
            if (path.IsNullOrWhiteSpace())
                return path;

            var sb = new StringBuilder(path.Length);
            foreach (var t in path)
            {
                var chr = t;
                if (chr == Path.AltDirectorySeparatorChar)
                    chr = Path.DirectorySeparatorChar;

                if (chr == Path.DirectorySeparatorChar && sb.Length > 0 && sb[^1] == Path.DirectorySeparatorChar)
                    continue;

                sb.Append(chr);
            }

            // For UNC and DOS device path support
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
                sb.Insert(0, @"\");

            return sb.ToString();
        }

        public static bool AreEqual(string? path1, string? path2)
        {
            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
                return false;

            try
            {
                path1 = Path.GetFullPath(path1).TrimEnd(DirectorySeparators);
                path2 = Path.GetFullPath(path2).TrimEnd(DirectorySeparators);
                return path1.Equals(path2, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string GetSafeFileName(string filename)
        {
            if (filename.IsNullOrWhiteSpace())
                return filename;

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(filename.Length);
            foreach (var chr in filename)
            {
                if (char.IsWhiteSpace(chr) && sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
                    continue;

                if (!invalid.Contains(chr))
                    sb.Append(chr);
            }

            return sb.ToString().Trim();
        }

        public static bool IsFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Don't use Path.IsPathRooted because it fails on paths starting with one backslash.
            return IsFullPathRegex().IsMatch(path);
        }

        public static string GetCommonDirectory(string[] paths)
        {
            var stop = paths.Min(a => a.Length);
            if (stop == 0)
                return string.Empty;

            foreach (var path in paths)
            {
                for (var j = 0; j < stop; j++)
                {
                    if (path[j] != paths[0][j])
                    {
                        stop = j;
                        goto cont;
                    }
                }
            }

            cont:
            var common = paths[0][..stop];
            if (common.Length == 0)
                return string.Empty;

            if (common[^1] == Path.DirectorySeparatorChar)
                return common;

            return common.Substring(0, common.LastIndexOf(Path.DirectorySeparatorChar) + 1);
        }

        public static string GetPathWithoutFileExtension(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path));
        }

        public static string GetFinalPathName(string path)
        {
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return path;
            }

            using var file = PInvokeWin32.CreateFile(path,
                0,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS);

            if (file.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            Span<char> text = new char[MaxPathLength];
            var res = PInvokeWin32.GetFinalPathNameByHandle(file, text, GETFINALPATHNAMEBYHANDLE_FLAGS.FILE_NAME_NORMALIZED);
            if (res == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var targetPath = text[..(int)res].ToString();
            if (targetPath.StartsWith(LongPathUncPrefix, StringComparison.Ordinal))
            {
                return targetPath.Replace(LongPathUncPrefix, @"\\", StringComparison.Ordinal);
            }

            return targetPath.Replace(LongPathPrefix, string.Empty, StringComparison.Ordinal);
        }
    }
}