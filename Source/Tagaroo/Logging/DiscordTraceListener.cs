using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Tagaroo.Discord;

namespace Tagaroo.Logging{
 internal class DiscordTraceListener : TraceListener{
  private readonly DiscordInterfacer Discord;
  private readonly TraceListener BackupTraceListener;
  private readonly StringBuilder CurrentLine=new StringBuilder();
  
  public DiscordTraceListener(string Name,DiscordInterfacer Discord,TraceListener BackupTraceListener)
  :base(Name){
   this.Discord=Discord;
   this.BackupTraceListener=BackupTraceListener;
  }
  
  public override void Write(string Message){
   CurrentLine.Append(Message);
  }

  public async override void WriteLine(string Message){
   if(CurrentLine.Length>0){
    Message = CurrentLine.Append(Message).ToString();
    CurrentLine.Clear();
   }
   if(!await Discord.LogMessage(Message)){
    BackupTraceListener.WriteLine(Message);
   }
  }
 }
}