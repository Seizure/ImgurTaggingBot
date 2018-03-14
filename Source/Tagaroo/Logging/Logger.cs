using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Tagaroo.Logging{
 /// <summary>
 /// <see cref="TraceSource"/> wrapper, adding convenience methods for easy logging.
 /// Also prepends timestamps to log messages, expressed in the host machine's local timezone.
 /// </summary>
 internal class Logger{
  private readonly TraceSource Implementation;
  public Logger(TraceSource Implementation){
   this.Implementation=Implementation;
  }

  public void LogCritical(string Message, params object[] MessageParameters){
   Message = AddTimestamp(Message);
   Implementation.TraceEvent(TraceEventType.Critical, 0, Message, MessageParameters);
  }

  public void LogError(string Message, params object[] MessageParameters){
   Message = AddTimestamp(Message);
   Implementation.TraceEvent(TraceEventType.Error, 0, Message, MessageParameters);
  }

  public void LogWarning(string Message, params object[] MessageParameters){
   Message = AddTimestamp(Message);
   Implementation.TraceEvent(TraceEventType.Warning, 0, Message, MessageParameters);
  }

  public void LogInfo(string Message, params object[] MessageParameters){
   Message = AddTimestamp(Message);
   Implementation.TraceEvent(TraceEventType.Information, 0, Message, MessageParameters);
  }

  public void LogVerbose(string Message, params object[] MessageParameters){
   Message = AddTimestamp(Message);
   Implementation.TraceEvent(TraceEventType.Verbose, 0, Message, MessageParameters);
  }

  public void Log(TraceEventType Level,string Message, params object[] MessageParameters){
   Message = AddTimestamp(Message);
   Implementation.TraceEvent(Level, 0, Message, MessageParameters);
  }

  public bool ShouldLog(TraceEventType Level){
   if(Implementation.Switch is null){return true;}
   return Implementation.Switch.ShouldTrace(Level);
  }

  internal void AddListener(TraceListener Add){
   Implementation.Listeners.Add(Add);
  }
  internal void RemoveListener(string Name){
   Implementation.Listeners.Remove(Name);
  }

  protected string AddTimestamp(string Message){
   return
    string.Format("[{0}] ",DateTimeOffset.Now.ToString(TimestampFormat))
    + Message ?? string.Empty
   ;
  }

  protected const string TimestampFormat="HH:mm:ss.fff";
 }
}