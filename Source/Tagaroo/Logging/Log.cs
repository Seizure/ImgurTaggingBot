using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Tagaroo.Logging{
 /// <summary>
 /// Singleton containing all the loggers for the application,
 /// also allowing configuration of each logger's verbosity level,
 /// and configuration of logging output.
 /// </summary>
 /// <remarks>
 /// For loggers written to by this application, the verbosity levels have the following meanings:
 /// <list type="bullet">
 /// <term>Critical</term> — <description>An error that could cause the application to halt, or have serious effects, that the administrator should be notified about.</description>
 /// <term>Error</term> — <description>An error preventing normal execution of the application.</description>
 /// <term>Warning</term> — <description>A recoverable mistake or potential mistake, made by a user.</description>
 /// <term>Information</term> — <description>Important informational messages about the running of the application. In general, this is the verbosity level that should be used in a deployment scenario.</description>
 /// <term>Verbose</term> — <description>Detailed information about the execution of the program. This verbosity level can be enabled to assist with fixing problems or locating the cause of problems.</description>
 /// </list>
 /// </remarks>
 internal class Log{
  /// <summary>
  /// Logging messages concerning application startup, setup, and shutdown, go to the Bootstrap logger.
  /// </summary>
  public Logger Bootstrap{get;}
  /// <summary>
  /// Logging messages from the Application Layer of the application go to the Application logger.
  /// </summary>
  public Logger Application{get;}
  /// <summary>
  /// Logging messages concerning the application's interaction with Imgur go to the Imgur logger.
  /// </summary>
  public Logger Imgur{get;}
  /// <summary>
  /// Logging messages concerning the status of the remaining Imgur API Bandwidth
  /// available to the application go to the Imgur Bandwidth logger.
  /// </summary>
  public Logger ImgurBandwidth{get;}
  /// <summary>
  /// Logging messages concerning the application's interaction with Discord go to the Discord logger.
  /// </summary>
  public Logger Discord{get;}
  /// <summary>
  /// The Discord.NET library also outputs logging information; this logging output is redirected to this logger.
  /// </summary>
  public Logger DiscordLibrary{get;}
  /// <summary>
  /// Logging verbosity level of the Bootstrap logger.
  /// </summary>
  public SourceSwitch BootstrapLevel{get;} = new SourceSwitch("BootstrapSwitch"){ Level=SourceLevels.Information };
  /// <summary>
  /// Logging verbosity level of the Application logger.
  /// </summary>
  public SourceSwitch ApplicationLevel{get;} = new SourceSwitch("ApplicationSwitch"){ Level=SourceLevels.Information };
  /// <summary>
  /// Logging verbosity level of the Imgur logger.
  /// </summary>
  public SourceSwitch ImgurLevel{get;} = new SourceSwitch("ImgurSwitch"){ Level=SourceLevels.Information };
  /// <summary>
  /// Logging verbosity level of the Imgur Bandwidth logger.
  /// </summary>
  public SourceSwitch ImgurBandwidthLevel{get;} = new SourceSwitch("ImgurBandwidthSwitch"){ Level=SourceLevels.Warning };
  /// <summary>
  /// Logging verbosity level of the Discord logger.
  /// </summary>
  public SourceSwitch DiscordLevel{get;} = new SourceSwitch("DiscordSwitch"){ Level=SourceLevels.Information };
  /// <summary>
  /// Logging verbosity level of the Discord Library logger;
  /// note that the meaning of the verbosity levels for this logger is different,
  /// as it is written to by the third-party Discord.NET library.
  /// As such, it should typically be set to Error in a deployment scenario.
  /// </summary>
  public SourceSwitch DiscordLibraryLevel{get;} = new SourceSwitch("DiscordLibrarySwitch"){ Level=SourceLevels.Information };
  
  public Log(){
   this.Bootstrap=new Logger(new TraceSource("Bootstrap"){
    Switch=this.BootstrapLevel
   });
   this.Application=new Logger(new TraceSource("Application"){
    Switch=this.ApplicationLevel
   });
   this.Imgur=new Logger(new TraceSource("Imgur"){
    Switch=this.ImgurLevel
   });
   this.ImgurBandwidth=new Logger(new TraceSource("ImgurBandwidth"){
    Switch=this.ImgurBandwidthLevel
   });
   this.Discord=new Logger(new TraceSource("Discord"){
    Switch=this.DiscordLevel
   });
   this.DiscordLibrary=new Logger(new TraceSource("DiscordLibrary"){
    Switch=this.DiscordLibraryLevel
   });
  }

  /// <summary>
  /// Adds a logging output destination to all loggers.
  /// </summary>
  /// <param name="IncludeDiscordLibraryLogger">
  /// Whether or not the DiscordLibrary logger should send logging output to the supplied destination;
  /// the DiscordLibrary logger should not send its logging output to Discord, as this causes re-entrancy issues
  /// </param>
  public void AddTraceListener(TraceListener Add,bool IncludeDiscordLibraryLogger){
   Bootstrap.AddListener(Add);
   Application.AddListener(Add);
   Imgur.AddListener(Add);
   ImgurBandwidth.AddListener(Add);
   Discord.AddListener(Add);
   if(IncludeDiscordLibraryLogger){
    DiscordLibrary.AddListener(Add);
   }
  }

  public void RemoveTraceListener(string Name){
   Bootstrap.RemoveListener(Name);
   Application.RemoveListener(Name);
   Imgur.RemoveListener(Name);
   ImgurBandwidth.RemoveListener(Name);
   Discord.RemoveListener(Name);
   DiscordLibrary.RemoveListener(Name);
  }

  static public Log Instance{get;} = new Log();

  /// <summary>
  /// <see cref="Bootstrap"/>
  /// </summary>
  static public Logger Bootstrap_ {get{return Instance.Bootstrap;}}
  /// <summary>
  /// <see cref="Application"/>
  /// </summary>
  static public Logger Application_ {get{return Instance.Application;}}
  /// <summary>
  /// <see cref="Imgur"/>
  /// </summary>
  static public Logger Imgur_ {get{return Instance.Imgur;}}
  /// <summary>
  /// <see cref="ImgurBandwidth"/>
  /// </summary>
  static public Logger ImgurBandwidth_ {get{return Instance.ImgurBandwidth;}}
  /// <summary>
  /// <see cref="Discord"/>
  /// </summary>
  static public Logger Discord_ {get{return Instance.Discord;}}
 }
}