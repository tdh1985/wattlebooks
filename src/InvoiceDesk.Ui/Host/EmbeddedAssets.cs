// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

using System.IO;
using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace InvoiceDesk.Ui.Host;

// the csproj names resources wwwroot/<path> but msbuild may use backslashes
public sealed class EmbeddedAssets : IFileProvider
{
    const string Prefix = "wwwroot/";

    public static EmbeddedAssets Instance { get; } = new(typeof(EmbeddedAssets).Assembly);

    readonly Assembly _assembly;
    readonly Dictionary<string, string> _resources;
    readonly DateTimeOffset _built;

    EmbeddedAssets(Assembly assembly)
    {
        _assembly = assembly;
        _built = File.GetLastWriteTimeUtc(AppContext.BaseDirectory);
        _resources = assembly.GetManifestResourceNames()
            .Where(n => n.Replace('\\', '/').StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(n => n.Replace('\\', '/')[Prefix.Length..], n => n, StringComparer.OrdinalIgnoreCase);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var key = subpath.Replace('\\', '/').TrimStart('/');
        return _resources.TryGetValue(key, out var resource)
            ? new ResourceFile(_assembly, resource, Path.GetFileName(key), _built)
            : new NotFoundFileInfo(key);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    public string ReadText(string subpath)
    {
        var file = GetFileInfo(subpath);
        if (!file.Exists) throw new FileNotFoundException($"Embedded asset {subpath} is missing.");
        using var reader = new StreamReader(file.CreateReadStream());
        return reader.ReadToEnd();
    }

    sealed class ResourceFile(Assembly assembly, string resource, string name, DateTimeOffset modified) : IFileInfo
    {
        public bool Exists => true;
        public long Length { get; } = assembly.GetManifestResourceStream(resource)?.Length ?? 0;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => modified;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => assembly.GetManifestResourceStream(resource)!;
    }
}
