namespace DbSeed.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    private TemporaryDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryDirectory Create()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dbseed-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path);
    }

    public string File(string fileName) => System.IO.Path.Combine(Path, fileName);

    public string Write(string fileName, string contents)
    {
        var file = File(fileName);
        System.IO.File.WriteAllText(file, contents);
        return file;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class CurrentDirectory : IDisposable
{
    private readonly string originalDirectory;

    private CurrentDirectory(string originalDirectory)
    {
        this.originalDirectory = originalDirectory;
    }

    public static CurrentDirectory ChangeTo(string path)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(path);
        return new CurrentDirectory(originalDirectory);
    }

    public void Dispose() => Directory.SetCurrentDirectory(originalDirectory);
}

internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter originalOut;
    private readonly TextWriter originalError;
    private readonly StringWriter outWriter = new();
    private readonly StringWriter errorWriter = new();

    private ConsoleCapture()
    {
        originalOut = Console.Out;
        originalError = Console.Error;
        Console.SetOut(outWriter);
        Console.SetError(errorWriter);
    }

    public string Out => outWriter.ToString();

    public string Error => errorWriter.ToString();

    public static ConsoleCapture Start() => new();

    public void Dispose()
    {
        Console.SetOut(originalOut);
        Console.SetError(originalError);
        outWriter.Dispose();
        errorWriter.Dispose();
    }
}
