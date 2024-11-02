using System;
using System.IO;

namespace CommonPlugin
{
    public class RelativePath
    {
        /// <summary>
        /// Returns a relative path string from a full path based on a base path
        /// provided.
        /// Found on https://weblog.west-wind.com/posts/2010/Dec/20/Finding-a-Relative-Path-in-NET and improved a little
        /// </summary>
        /// <param name="basePath">The base path on which relative processing is based. Should be a directory.</param>
        /// <param name="fullPath">The path to convert. Can be either a file or a directory</param>
        /// <returns>
        /// String of the relative path.
        /// 
        /// Examples of returned values:
        ///  test.txt, ..\test.txt, ..\..\..\test.txt, ., .., subdir\test.txt
        /// </returns>
        public static string Get(string basePath, string fullPath)
        {
            // Require trailing backslash for path
            if (!basePath.EndsWith("\\"))
                basePath += "\\";

            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

            // Uri's use forward slashes so convert back to backward slashes
            // Uri's also escape some chars, co convert back to unescaped format
            return Uri.UnescapeDataString(relativeUri.ToString().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        }
    }
}
