namespace CICD.Tools.PackageSignTests
{
    using System.Reflection;

    using Microsoft.Extensions.Logging;

    using Skyline.DataMiner.CICD.FileSystem;

    public static class TestHelper
    {
        public static string GetTestFilesDirectory()
        {
            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return FileSystem.Instance.Path.Combine(baseDir, "Test Files");
        }

        public static TestLogger GetTestLogger()
        {
            return new TestLogger();
        }
    }

    public class TestLogger : ILogger
    {
        public List<string> ErrorLogging { get; } = [];

        public List<string> Logging { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            string invoke = formatter.Invoke(state, exception);
            Logging.Add($"{logLevel}: [{eventId}] {invoke}");

            if (logLevel == LogLevel.Error)
            {
                ErrorLogging.Add($"[{eventId}] {invoke}");
            }
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            throw new NotImplementedException();
        }
    }
}