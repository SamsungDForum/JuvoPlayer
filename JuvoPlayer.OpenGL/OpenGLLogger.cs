using System.Threading;
using JuvoLogger;

namespace JuvoPlayer.OpenGL
{
    public class OpenGLLogger : LoggerBase
    {
        private readonly SynchronizationContext _uiContext = null;

        public OpenGLLogger(string channel, LogLevel level, SynchronizationContext uiContext) : base(channel, level)
        {
            _uiContext = uiContext;
        }

        public override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            if (string.IsNullOrEmpty(message) || level > this.Level)
                return;
            _uiContext.Post(state => { Program.PushLog(message); }, null);
        }
    }
}
