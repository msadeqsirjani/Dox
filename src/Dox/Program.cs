namespace Dox;

public class Program
{
    private static bool _showCancelHelp = true;

    public static int Main(string[] args)
    {
        return Parser.Default.ParseArguments<UploadOptions>(args).MapResult(RunAndReturnExitCode, _ => (int)ExitCode.BadArguments);
    }

    private static int RunAndReturnExitCode(UploadOptions options)
    {
        if (options.Reset)
        {
            DropboxClientFactory.ResetAuthentication();
        }

        var source = Path.GetFullPath(options.LocalPath);

        if (!File.Exists(source) && !Directory.Exists(source))
        {
            Console.WriteLine("Source does not exist.");
            return (int)ExitCode.FileNotFound;
        }

        // Fix up Dropbox path (fix Windows-style slashes)
        options.DropboxPath = options.DropboxPath.Replace(@"\", "/");

        string[] files;

        // Determine whether source is a file or directory
        var attributes = File.GetAttributes(source);
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            // TODO see if we like what this looks like for directories
            Output($"Uploading folder \"{source}\" to {(!string.IsNullOrEmpty(options.DropboxPath) ? options.DropboxPath : "Dropbox")}", options);
            Output("Ctrl-C to cancel", options);
            _showCancelHelp = false;

            // TODO Figure out what, if anything, we want to do about subdirectories
            files = Directory.GetFiles(source);
        }
        else
        {
            files = new[] { source };
        }

        var exitCode = ExitCode.UnknownError;

        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            var client = DropboxClientFactory.CreateDropboxClient(options.TimeoutSeconds).Result;
            var task = Task.Run(() => Upload(files, options, client, cts.Token), cts.Token);
            task.Wait(cts.Token);
            exitCode = ExitCode.Success;
        }
        catch (OperationCanceledException)
        {
            Output("\nUpload canceled", options);

            exitCode = ExitCode.Canceled;
        }
        catch (AggregateException ex)
        {
            foreach (var exception in ex.Flatten().InnerExceptions)
            {
                exitCode = exception switch
                {
                    DropboxException dex => HandleDropboxException(dex),
                    TaskCanceledException tex => HandleTimeoutError(tex),
                    _ => HandleGenericError(ex)
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred and your file was not uploaded.");
            Console.WriteLine(ex);
        }

        return (int)exitCode;
    }

    private static async Task Upload(IEnumerable<string> paths, UploadOptions options, DropboxClient client,
        CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            var source = Path.GetFullPath(path);
            var filename = Path.GetFileName(source);

            await Upload(source, filename, options, client, cancellationToken);
        }
    }

    private static async Task Upload(string source, string filename, UploadOptions options, DropboxClient client,
        CancellationToken cancellationToken)
    {
        Output($"Uploading {filename} to {options.DropboxPath}", options);
        Console.Title = $"Uploading {filename} to {(!string.IsNullOrEmpty(options.DropboxPath) ? options.DropboxPath : "Dropbox")}";

        if (_showCancelHelp)
        {
            Output("Ctrl-C to cancel", options);
            _showCancelHelp = false;
        }

        await using var stream = new FileStream(source, FileMode.Open, FileAccess.Read);

        Metadata? uploaded;

        if (!options.Chunked && stream.Length >= DropboxClientExtensions.ChunkedThreshold)
        {
            Output("File is larger than 150MB, using chunked uploading.", options);
            options.Chunked = true;
        }

        if (options.Chunked && stream.Length <= options.ChunkSize)
        {
            Output("File is smaller than the specified chunk size, disabling chunked uploading.", options);
            options.Chunked = false;
        }

        if (options.Chunked)
        {
            var progress = ConfigureProgressHandler(options, stream.Length);
            uploaded = await client.UploadChunked(options.DropboxPath, filename, stream, options.ChunkSize, progress, cancellationToken);
        }
        else
        {
            uploaded = await client.Upload(options.DropboxPath, filename, stream);
        }

        Output("Whoosh...", options);
        Output($"Uploaded {uploaded?.Name} to {uploaded?.PathDisplay}; Revision {uploaded?.AsFile.Rev}", options);
    }

    private static void Output(string message, UploadOptions options)
    {
        if (options.Quiet)
        {
            return;
        }

        Console.WriteLine(message);
    }

    private static IProgress<long> ConfigureProgressHandler(UploadOptions options, long fileSize)
    {
        if (options.NoProgress || options.Quiet)
        {
            return new NoProgressDisplay(fileSize, options.Quiet);
        }

        if (options.Bytes)
        {
            return new BytesProgressDisplay(fileSize);
        }

        return new PercentProgressDisplay(fileSize);
    }

    private static ExitCode HandleDropboxException(DropboxException ex)
    {
        Console.WriteLine("An error occurred and your file was not uploaded.");

        var exitCode = ex.HandleAuthenticationException() ??
                       ex.HandleAccessException() ??
                       ex.HandleRateLimitException() ??
                       ex.HandleBadInputException() ??
                       ex.HandleHttpException() ??
                       ExitCode.UnknownError;

        if (exitCode == ExitCode.UnknownError)
        {
            Console.WriteLine(ex.Message);
        }

        return exitCode;
    }

    private static ExitCode HandleGenericError(Exception ex)
    {
        Console.WriteLine("An error occurred and your file was not uploaded.");
        Console.WriteLine(ex);

        return ExitCode.UnknownError;
    }

    private static ExitCode HandleTimeoutError(TaskCanceledException ex)
    {
        Console.WriteLine("An HTTP operation timed out and your file was not uploaded.");
        Console.WriteLine(ex);

        return ExitCode.Canceled;
    }

}