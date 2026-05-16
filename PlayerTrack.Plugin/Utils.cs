using System;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Text.ReadOnly;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using PlayerTrack.Resource;

namespace PlayerTrack;

public static class Utils
{
    public static CultureInfo CurrentCulture = CultureInfo.InvariantCulture;

    /// <summary>
    /// Get the localized string for a resource key.
    /// </summary>
    /// <param name="key">Localization resource name.</param>
    /// <returns>The localized string, or the original if not found.</returns>
    public static string GetLoc(string key)
    {
        return Language.ResourceManager.GetString(key, CurrentCulture) ?? key;
    }

    /// <summary>
    /// Sanitize string to remove unprintable characters (short hand method).
    /// </summary>
    /// <param name="seString">A lumina SeString to sanitize.</param>
    /// <returns>Indicator if player character is valid.</returns>
    public static string Sanitize(ReadOnlySeString seString)
    {
        return Plugin.PluginInterface.Sanitizer.Sanitize(seString.ExtractText());
    }

    /// <summary>
    /// Convert a UI color to a Vector4.
    /// </summary>
    /// <param name="col">color.</param>
    /// <returns>vector4.</returns>
    public static Vector4 UiColorToVector4(uint col)
    {
        var fa = (col & 255) / 255f;
        var fb = (col >> 8 & 255) / 255f;
        var fg = (col >> 16 & 255) / 255f;
        var fr = (col >> 24 & 255) / 255f;
        return new Vector4(fr, fg, fb, fa);
    }

    /// <summary>
    /// Checks if two colors are similar in tone.
    /// </summary>
    /// <param name="a">color</param>
    /// <param name="b">color</param>
    /// <param name="tolerance">similarity tolerance</param>
    /// <returns>true if similar</returns>
    public static bool AreColorsSimilar(Vector4 a, Vector4 b, float tolerance = 0.05f) =>
        Math.Abs(a.X - b.X) < tolerance && Math.Abs(a.Y - b.Y) < tolerance && Math.Abs(a.Z - b.Z) < tolerance;


    /// <summary>
    /// Calculates the similarity distance between two colors.
    /// </summary>
    /// <param name="a">color</param>
    /// <param name="b">color</param>
    /// <returns>true if similar</returns>
    public static float ColorDistance(Vector4 a, Vector4 b)
    {
        var rDiff = a.X - b.X;
        var gDiff = a.Y - b.Y;
        var bDiff = a.Z - b.Z;
        var aDiff = a.W - b.W;

        return (float)Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff + aDiff * aDiff);
    }

    /// <summary>
    /// Converts a color Vector4 to Hue.
    /// </summary>
    /// <param name="color">color</param>
    /// <returns>hue as float</returns>
    public static float ColorToHue(Vector4 color)
    {
        var r = color.X;
        var g = color.Y;
        var b = color.Z;

        var min = Math.Min(r, Math.Min(g, b));
        var max = Math.Max(r, Math.Max(g, b));

        var delta = max - min;
        var hue = 0f;

        if (!(Math.Abs(delta) > float.Epsilon))
            return hue;

        if (Math.Abs(max - r) < float.Epsilon)
            hue = (g - b) / delta;
        else if (Math.Abs(max - g) < float.Epsilon)
            hue = 2 + ((b - r) / delta);
        else if (Math.Abs(max - b) < float.Epsilon)
            hue = 4 + ((r - g) / delta);

        return ((hue * 60) + 360) % 360;
    }

    /// <summary>
    /// Submit a command to the game's chat box as if the user typed it.
    /// Used for game-side commands (e.g. /search) that Dalamud's CommandManager
    /// does not dispatch.
    /// </summary>
    public static unsafe void SendGameChatCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        if (command.Length > 500) return;

        var ui = UIModule.Instance();
        if (ui == null) return;

        var utf8 = Utf8String.FromString(command);
        if (utf8 == null) return;
        try
        {
            ui->ProcessChatBoxEntry(utf8, 0);
        }
        finally
        {
            utf8->Dtor(true);
        }
    }

    /// <summary>
    /// Get the plugin version as number.
    /// </summary>
    /// <param name="value">Dalamud plugin interface.</param>
    /// <returns>Plugin version as int.</returns>
    public static int GetPluginVersion(this IDalamudPluginInterface value)
    {
        var version = value.IsTesting ? value.Manifest.TestingAssemblyVersion : value.Manifest.AssemblyVersion;
        return (version!.Major * 1000000) + (version.Minor * 10000) + (version.Build * 100) + version.Revision;
    }
}

/// <summary>
/// A helper class for file operations.
/// </summary>
public static class FileHelper
{
    /// <summary>
    /// Moves the specified file from the source directory to the destination directory and compresses into a zip file.
    /// </summary>
    /// <param name="sourceDirectory">The source directory.</param>
    /// <param name="fileName">File name including extension.</param>
    /// <param name="destinationDirectory">The destination directory.</param>
    public static void MoveAndCompressFile(string sourceDirectory, string fileName, string destinationDirectory) =>
        MoveAndCompressFiles(sourceDirectory, new List<string> { fileName }, destinationDirectory, $"{fileName}.zip");

    /// <summary>
    /// Moves the specified files from the source directory to the destination directory and compresses them into a zip
    /// file.
    /// </summary>
    /// <param name="sourceDirectory">The source directory.</param>
    /// <param name="fileNames">The list of file names including extensions.</param>
    /// <param name="destinationDirectory">The destination directory.</param>
    /// <param name="outputZipFileName">The output zip file name.</param>
    public static void MoveAndCompressFiles(string sourceDirectory, List<string> fileNames, string destinationDirectory, string outputZipFileName)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var tempDirectory = Path.Combine(destinationDirectory, "temp");
        if (!Directory.Exists(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }

        foreach (var fileName in fileNames)
        {
            var sourceFile = Path.Combine(sourceDirectory, fileName);
            var destinationFile = Path.Combine(tempDirectory, fileName);

            if (File.Exists(sourceFile))
            {
                File.Move(sourceFile, destinationFile);
            }
        }

        var zipFilePath = Path.Combine(destinationDirectory, outputZipFileName);
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        ZipFile.CreateFromDirectory(tempDirectory, zipFilePath);

        Directory.Delete(tempDirectory, true);
    }

    /// <summary>
    /// Compresses the specified file into a zip file with the given destination file name and removes the original
    /// uncompressed file.
    /// </summary>
    /// <param name="filePath">The path of the file to compress.</param>
    /// <param name="destinationFileName">The name of the compressed zip file.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public static void CompressFile(string filePath, string destinationFileName)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        var fileName = Path.GetFileName(filePath);
        var destinationFile = Path.Combine(tempDirectory, fileName);

        File.Move(filePath, destinationFile);

        var zipFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", destinationFileName);

        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        ZipFile.CreateFromDirectory(tempDirectory, zipFilePath);

        Directory.Delete(tempDirectory, true);
    }

    /// <summary>
    /// Moves all files from the source directory to the destination directory and compresses them into a zip file.
    /// </summary>
    /// <param name="sourceDirectory">The source directory.</param>
    /// <param name="destinationDirectory">The destination directory.</param>
    /// <param name="outputZipFileName">The output zip file name.</param>
    public static void MoveAndCompressDirectory(string sourceDirectory, string destinationDirectory, string outputZipFileName)
    {
        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var zipFilePath = Path.Combine(destinationDirectory, outputZipFileName);
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath);
        }

        ZipFile.CreateFromDirectory(sourceDirectory, zipFilePath);

        if (File.Exists(zipFilePath))
        {
            Directory.Delete(sourceDirectory, true);
        }
    }

    /// <summary>
    /// Decompresses the files from the specified zip file and deletes the compressed version.
    /// </summary>
    /// <param name="zipFilePath">The path to the zip file.</param>
    /// <param name="destinationDirectory">The destination directory to extract the files.</param>
    /// <param name="deleteZipFile">Whether to delete zip file after.</param>
    public static void MoveAndDecompressFiles(string zipFilePath, string destinationDirectory, bool deleteZipFile = true)
    {
        if (File.Exists(zipFilePath))
        {
            ZipFile.ExtractToDirectory(zipFilePath, destinationDirectory);
            if (deleteZipFile)
            {
                File.Delete(zipFilePath);
            }
        }
        else
        {
            throw new FileNotFoundException($"Zip file does not exist: {zipFilePath}.");
        }
    }

    /// <summary>
    /// Verifies if the application has read and write access to a specified file.
    /// </summary>
    /// <param name="fileName">The name or path of the file to check.</param>
    /// <returns>True if the application has read and write access, otherwise false.</returns>
    public static bool VerifyFileAccess(string fileName)
    {
        try
        {
            // If the file does not exist, assume we have access to create it
            if (!File.Exists(fileName))
            {
                return true;
            }

            // If the file exists, try to open it with write access and sharing read access
            using (new FileStream(fileName, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                // If successful, we have write access to the file
                return true;
            }
        }
        catch (Exception)
        {
            // If an exception occurs, we don't have access to the file
            return false;
        }
    }
}
