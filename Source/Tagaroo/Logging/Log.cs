using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Tagaroo.Logging{
 internal class Log{
  public Logger Bootstrap{get;}
  public Logger Application{get;}
  public Logger Imgur{get;}
  public Logger ImgurBandwidth{get;}
  public Logger Discord{get;}
  public Logger DiscordLibrary{get;}
  public SourceSwitch BootstrapLevel{get;} = new SourceSwitch("BootstrapSwitch"){ Level=SourceLevels.Information };
  public SourceSwitch ApplicationLevel{get;} = new SourceSwitch("ApplicationSwitch"){ Level=SourceLevels.Information };
  public SourceSwitch ImgurLevel{get;} = new SourceSwitch("ImgurSwitch"){ Level=SourceLevels.Information };
  public SourceSwitch ImgurBandwidthLevel{get;} = new SourceSwitch("ImgurBandwidthSwitch"){ Level=SourceLevels.Warning };
  public SourceSwitch DiscordLevel{get;} = new SourceSwitch("DiscordSwitch"){ Level=SourceLevels.Information };
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

  public void AddTraceListener(TraceListener Add){
   Bootstrap.AddListener(Add);
   Application.AddListener(Add);
   Imgur.AddListener(Add);
   ImgurBandwidth.AddListener(Add);
   Discord.AddListener(Add);
   DiscordLibrary.AddListener(Add);
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

  static public Logger Bootstrap_ {get{return Instance.Bootstrap;}}
  static public Logger Application_ {get{return Instance.Application;}}
  static public Logger Imgur_ {get{return Instance.Imgur;}}
  static public Logger ImgurBandwidth_ {get{return Instance.ImgurBandwidth;}}
  static public Logger Discord_ {get{return Instance.Discord;}}
 }
}