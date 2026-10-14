namespace FivePRS.Server
{
    internal readonly struct CommandResult
    {
        private CommandResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message   = message;
        }

        public bool Succeeded { get; }

        public string Message { get; }

        public static CommandResult Done(string message) => new(true, message);

        public static CommandResult Failed(string message) => new(false, message);
    }
}
