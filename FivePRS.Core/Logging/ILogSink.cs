namespace FivePRS.Core.Logging
{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }
}
