using System;
using System.IO;

namespace Tagaroo{
 public class CrashHandler{
  private readonly TextWriter Out;
  private readonly AppDomain HandleCrashesFrom;
  /// <param name="HandleCrashesFrom">
  /// The <see cref="AppDomain"/> from which any unhandled exceptions causing process crash should be output
  /// </param>
  /// <param name="OutputCrashDetailsTo">
  /// Where details of unhandled exceptions causing a process crash are written to;
  /// the supplied <see cref="TextWriter"/> instance should not throw any exceptions
  /// </param>
  public CrashHandler(AppDomain HandleCrashesFrom,TextWriter OutputCrashDetailsTo){
   if(OutputCrashDetailsTo is null){throw new ArgumentNullException(nameof(OutputCrashDetailsTo));}
   this.Out=OutputCrashDetailsTo;
   this.HandleCrashesFrom=HandleCrashesFrom;
   HandleCrashesFrom.UnhandledException += OutputUnhandledExceptionDetails;
  }

  private void OutputUnhandledExceptionDetails(Object Origin,UnhandledExceptionEventArgs Event){
   if(!Event.IsTerminating){return;}
   Out.WriteLine("--- Process Crash ---");
   Out.WriteLine(DateTimeOffset.Now.ToString("yyyy'-'MM'-'dd HH':'mm':'ss'.'fff"));
   if(!(Event.ExceptionObject is Exception)){
    Out.WriteLine("Non-Exception thrown:");
    Out.WriteLine("{0}",Event.ExceptionObject);
    Out.WriteLine();
    return;
   }
   OutputExceptionDetails(string.Empty,(Exception)Event.ExceptionObject);
   Out.WriteLine();
   Out.WriteLine("--- Process Terminating ---");
   Out.Close();
  }

  protected void OutputExceptionDetails(string Path,Exception Display){
   if(Display is null){return;}
   Out.WriteLine();
   Out.WriteLine("{0}{1}:",Path,Display.GetType().Name);
   Out.WriteLine("Type - {0}",Display.GetType().FullName);
   Out.WriteLine("Message - {0}",Display.Message ?? "<null>");
   Out.WriteLine("Source - {0}",Display.Source ?? "<null>");
   if(!(Display.TargetSite is null)){
    Out.WriteLine("Thrown from - {0}",Display.TargetSite);
   }
   if(!(Display.StackTrace is null)){
    Out.WriteLine("Stack-");
    Out.WriteLine(Display.StackTrace);
   }
   //Out.WriteLine(Display.ToString());
   if(!(Display.InnerException is null)){
    OutputExceptionDetails(
     Path + Display.GetType().Name + ".",
     Display.InnerException
    );
   }
   AggregateException DisplayAggregate = Display as AggregateException;
   if(!(DisplayAggregate is null) && !(DisplayAggregate.InnerExceptions is null)){
    for(int index=0;index<DisplayAggregate.InnerExceptions.Count;++index){
     OutputExceptionDetails(
      string.Format("{0}{1}[{2:D}].", Path, Display.GetType().Name, index),
      DisplayAggregate.InnerExceptions[index]
     );
    }
   }
  }

  public void UnRegister(){
   HandleCrashesFrom.UnhandledException -= OutputUnhandledExceptionDetails;
  }
 }
}