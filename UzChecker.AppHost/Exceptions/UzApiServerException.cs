namespace UzChecker.AppHost.Exceptions
{
    public class UzApiServerException : Exception
    {
        public int StatusCode { get; }

        public UzApiServerException(string message, int statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}

