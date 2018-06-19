using System.Threading;
using JuvoLogger;

namespace JuvoPlayer.OpenGL
{
    public class OpenGLLogger : LoggerBase
    {
        private readonly SynchronizationContext _synchronizationContext = SynchronizationContext.Current;

        public OpenGLLogger(string channel, LogLevel level) : base(channel, level)
        {
        }

        public override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            if (string.IsNullOrEmpty(message) || level > this.Level)
                return;
            _synchronizationContext.Post(state => { Program.PushLog(message); }, null);
        }
    }
}
