using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Tagaroo.Logging{
 internal class Logger{
  private readonly TraceSource Implementation;
  public Logger(TraceSource Implementation){
   this.Implementation=Implementation;
  }

  public void LogCritical(string Message, params object[] MessageParameters){
   Implementation.TraceEvent(TraceEventType.Critical, 0, Message, MessageParameters);
  }

  public void LogError(string Message, params object[] MessageParameters){
   Implementation.TraceEvent(TraceEventType.Error, 0, Message, MessageParameters);
  }

  public void LogWarning(string Message, params object[] MessageParameters){
   Implementation.TraceEvent(TraceEventType.Warning, 0, Message, MessageParameters);
  }

  public void LogInfo(string Message, params object[] MessageParameters){
   Implementation.TraceEvent(TraceEventType.Information, 0, Message, MessageParameters);
  }

  public void LogVerbose(string Message, params object[] MessageParameters){
   Implementation.TraceEvent(TraceEventType.Verbose, 0, Message, MessageParameters);
  }

  internal void AddListener(TraceListener Add){
   Implementation.Listeners.Add(Add);
  }
  internal void RemoveListener(string Name){
   Implementation.Listeners.Remove(Name);
  }
 }
}