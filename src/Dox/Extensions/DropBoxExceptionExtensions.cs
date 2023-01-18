namespace Dox.Extensions;

public static class DropBoxExceptionExtensions
{
    public static ExitCode? HandleAuthenticationException(this DropboxException exception)
    {
        if (exception is not AuthException authenticationException) return null;

        Console.WriteLine($"An authentication error occurred: {authenticationException}");

        return ExitCode.AccessDenied;
    }

    public static ExitCode? HandleAccessException(this DropboxException exception)
    {
        if (exception is not AccessException accessException) return null;

        Console.WriteLine($"An access error occurred: {accessException}");

        return ExitCode.AccessDenied;
    }

    public static ExitCode? HandleRateLimitException(this DropboxException exception)
    {
        if (exception is not RateLimitException rateLimitException) return null;

        Console.WriteLine($"An access error occurred: {rateLimitException}");

        return ExitCode.Canceled;
    }

    public static ExitCode? HandleBadInputException(this DropboxException exception)
    {
        if (exception is not BadInputException badInputException) return null;

        Console.WriteLine($"An access error occurred: {badInputException}");

        return ExitCode.BadArguments;
    }

    public static ExitCode? HandleHttpException(this DropboxException exception)
    {
        if (exception is not HttpException httpException) return null;

        Console.WriteLine($"An access error occurred: {httpException}");

        return ExitCode.BadArguments;
    }
}