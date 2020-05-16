namespace WebApi
{
    using Microsoft.Extensions.Logging;

    using Serilog;
    using Serilog.Context;

    public static class Logger
    {
        public static void Write(LogLevel level, ILog data)
        {
            switch (level)
            {
                case LogLevel.Critical:
                    Log.Fatal("{@data}", data);
                    break;
                case LogLevel.Error:
                    Log.Error("{@data}", data);
                    break;
                case LogLevel.Warning:
                    Log.Warning("{@data}", data);
                    break;
                case LogLevel.Information:
                    Log.Information("{@data}", data);
                    break;
                case LogLevel.Debug:
                    Log.Debug("{@data}", data);
                    break;
                case LogLevel.Trace:
                    Log.Verbose("{@data}", data);
                    break;
            }
        }
    }
}
