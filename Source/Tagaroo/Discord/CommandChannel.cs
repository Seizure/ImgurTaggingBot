using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Tagaroo.Logging;

namespace Tagaroo.Discord{
 public class CommandChannel{
  private readonly DiscordCommandInterfacer Discord;
  //private readonly ulong GuildID;
  private readonly ulong ChannelID;
  private readonly string CommandPrefix;
  
  public CommandChannel(
   DiscordCommandInterfacer Discord,
   ulong ChannelID,
   string CommandPrefix
  ){
   this.Discord=Discord;
   //this.GuildID=GuildID;
   this.ChannelID=ChannelID;
   this.CommandPrefix=CommandPrefix;
  }

  public async Task MessageReceived(SocketUserMessage Message){
   if(Message.Channel.Id!=ChannelID){
    return;
   }
   int CommandStartOffset=0;
   if(!Message.HasStringPrefix( CommandPrefix, ref CommandStartOffset, StringComparison.CurrentCultureIgnoreCase )){
    return;
   }
   IResult CommandResult=await Discord.ExecuteCommand(Message, CommandStartOffset);
   if(!CommandResult.IsSuccess){
    try{
     await Discord.SendMessage(
      ChannelID,
      string.Format("{0}: {1}",CommandResult.Error,CommandResult.ErrorReason),
      false
     );
    }catch(DiscordException){
     //Log.Discord_.LogVerbose();
     return;
    }
   }
  }
 }
}