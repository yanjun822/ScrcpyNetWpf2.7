 
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrcpyNetWpf2._7
{
    internal class LogServiceSink : ILogEventSink
    {
        private readonly IFormatProvider _formatProvider;
        public LogServiceSink(IFormatProvider formatProvider = null)
        {
            _formatProvider = formatProvider;
        }

        public void Emit(LogEvent logEvent)
        {
             var message = logEvent.RenderMessage(_formatProvider);
            Console.WriteLine($"{message},{logEvent.Exception}");

         

        }
    }
}
 