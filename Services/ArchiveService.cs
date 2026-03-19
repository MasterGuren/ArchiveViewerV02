using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;

namespace ArchiveViewer.Services;

public interface IArchiveReader : IDisposable
{
    List<string> GetImageNames();
    byte[] ReadEntry(string name);
}

public class ZipArchiveReader : IArchiveReader
{
    private readonly ZipArchive _archive;

    public ZipArchiveReader(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            _archive = ZipFile.Open(path, ZipArchiveMode.Read, Encoding.GetEncoding(932));
        }
        catch
        {
            _archive = ZipFile.Open(path, ZipArchiveMode.Read);
        }
    }

    public List<string> GetImageNames()
    {
        return _archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Where(e => !e.FullName.StartsWith("__MACOSX", StringComparison.OrdinalIgnoreCase))
            .Where(e => Theme.ImageExtensions.Contains(Path.GetExtension(e.Name).ToLowerInvariant()))
            .Select(e => e.FullName)
            .OrderBy(n => n, NaturalStringComparer.Instance)
            .ToList();
    }

    public byte[] ReadEntry(string name)
    {
        var entry = _archive.GetEntry(name);
        if (entry == null) return [];
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public void Dispose() => _archive.Dispose();
}

public class RarArchiveReader : IArchiveReader
{
    private readonly RarArchive _archive;

    public RarArchiveReader(string path)
    {
        _archive = RarArchive.Open(path);
    }

    public List<string> GetImageNames()
    {
        return _archive.Entries
            .Where(e => !e.IsDirectory)
            .Where(e => Theme.ImageExtensions.Contains(Path.GetExtension(e.Key ?? "").ToLowerInvariant()))
            .Select(e => e.Key ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n, NaturalStringComparer.Instance)
            .ToList();
    }

    public byte[] ReadEntry(string name)
    {
        var entry = _archive.Entries.FirstOrDefault(e => e.Key == name);
        if (entry == null) return [];
        using var stream = entry.OpenEntryStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public void Dispose() => _archive.Dispose();
}

public class SevenZipArchiveReader : IArchiveReader
{
    private const string SevenZipPath = @"C:\Program Files\7-Zip\7z.exe";
    private readonly string _archivePath;
    private List<string>? _names;

    public SevenZipArchiveReader(string path)
    {
        _archivePath = path;
        if (!File.Exists(SevenZipPath))
            throw new FileNotFoundException("7-Zip not found at " + SevenZipPath);
    }

    public List<string> GetImageNames()
    {
        if (_names != null) return _names;

        var psi = new ProcessStartInfo
        {
            FileName = SevenZipPath,
            Arguments = $"l -slt \"{_archivePath}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        _names = [];
        foreach (var block in output.Split("----------", StringSplitOptions.RemoveEmptyEntries))
        {
            string? path = null;
            bool isDir = false;
            foreach (var line in block.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Path = "))
                    path = trimmed["Path = ".Length..];
                else if (trimmed.StartsWith("Folder = +"))
                    isDir = true;
            }
            if (path != null && !isDir)
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (Theme.ImageExtensions.Contains(ext))
                    _names.Add(path);
            }
        }

        _names.Sort(NaturalStringComparer.Instance);
        return _names;
    }

    public byte[] ReadEntry(string name)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SevenZipPath,
            Arguments = $"e -so \"{_archivePath}\" \"{name}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        using var ms = new MemoryStream();
        proc.StandardOutput.BaseStream.CopyTo(ms);
        proc.WaitForExit();
        return ms.ToArray();
    }

    public void Dispose() { }
}

public static class ArchiveFactory
{
    public static IArchiveReader Open(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".zip" or ".cbz" => new ZipArchiveReader(path),
            ".rar" or ".cbr" => OpenRar(path),
            ".7z" => new SevenZipArchiveReader(path),
            _ => throw new NotSupportedException($"Unsupported archive format: {ext}")
        };
    }

    private static IArchiveReader OpenRar(string path)
    {
        try
        {
            return new RarArchiveReader(path);
        }
        catch
        {
            return new SevenZipArchiveReader(path);
        }
    }
}

public class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return StrCmpLogicalW(x, y);
    }
}
