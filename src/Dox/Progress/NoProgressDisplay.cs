namespace Dox.Progress;

public class NoProgressDisplay : IProgress<long>
{
    private readonly long _fileSize;
    private readonly bool _quiet;

    public NoProgressDisplay(long fileSize, bool quiet)
    {
        _fileSize = fileSize;
        _quiet = quiet;
    }

    public void Report(long value)
    {
        if (value >= _fileSize && !_quiet)
        {
            Console.Write("Finished\n");
        }
    }
}