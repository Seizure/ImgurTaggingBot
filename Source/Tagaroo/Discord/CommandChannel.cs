using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Tagaroo.Logging;

using HttpException=Discord.Net.HttpException;
using HttpRequestException=System.Net.Http.HttpRequestException;

namespace Tagaroo.Discord{
 /// <summary>
 /// Component of <see cref="DiscordInterfacer"/>;
 /// represents an individual Discord Channel where Discord commands are accepted from.
 /// At present, <see cref="DiscordInterfacer"/> is only associated with one <see cref="CommandChannel"/>;
 /// however this design could be extended to allow multiple Channels where commands are accepted from,
 /// each Channel allowing a different set of commands,
 /// for finer-grained authorization of command usage.
 /// </summary>
 public class CommandChannel{
  //private readonly ulong GuildID;
  private readonly ulong ChannelID;
  private readonly string CommandPrefix;
  private readonly CommandService CommandExecuter;
  private readonly DiscordSocketClient Client;
  private IServiceProvider CommandServices;
  
  /// <param name="ChannelID">The numeric ID of the Discord Channel from which commands will be accepted and processed</param>
  /// <param name="CommandExecuter">The Discord.Net library service that handles command processing</param>
  /// <param name="Client">Discord.Net library object required for interacting with <paramref name="CommandExecuter"/></param>
  /// <param name="CommandPrefix">The string prefix that all commands issued to this Channel must begin with to be treated as commands</param>
  public CommandChannel(
   ulong ChannelID,
   CommandService CommandExecuter,
   DiscordSocketClient Client,
   string CommandPrefix
  ){
   if(CommandExecuter is null){throw new ArgumentNullException(nameof(CommandExecuter));}
   if(Client is null){throw new ArgumentNullException(nameof(Client));}
   //this.GuildID=GuildID;
   this.ChannelID=ChannelID;
   this.CommandExecuter=CommandExecuter;
   this.Client=Client;
   this.CommandPrefix = CommandPrefix ?? string.Empty;
  }

  /// <summary>
  /// <para>Preconditions: Has not already been called</para>
  /// Must be called before other methods, to complete construction of the object.
  /// </summary>
  /// <param name="CommandServices">Discord.Net library object required for interacting with the associated <see cref="CommandService"/></param>
  public void Initialize(IServiceProvider CommandServices){
   if(CommandServices is null){throw new ArgumentNullException();}
   if(!(this.CommandServices is null)){throw new InvalidOperationException();}
   this.CommandServices=CommandServices;
  }

  /// <summary>
  /// <para>Preconditions: <see cref="Initialize"/> has been called</para>
  /// Should be called whenever a Discord message is received;
  /// any messages that are not from the associated Channel are automatically discarded,
  /// though callers may discard them without making the call.
  /// If the message is from the associated Channel,
  /// checks to see if it contains a command, which must begin with the specified string prefix,
  /// and if so passes it to the associated <see cref="CommandService"/> for processing.
  /// </summary>
  public async Task MessageReceived(SocketUserMessage Message){
   if(this.CommandServices is null){throw new InvalidOperationException("Not initialized");}
   if(Message.Channel.Id!=ChannelID){
    return;
   }
   int CommandStartOffset=0;
   if(!Message.HasStringPrefix( CommandPrefix, ref CommandStartOffset, StringComparison.CurrentCultureIgnoreCase )){
    return;
   }
   Log.Discord_.LogVerbose("Received command message on Command Channel #{0:D} from user '{1}' starting at offset {2}",ChannelID,Message.Author,CommandStartOffset);
   IResult CommandResult = await CommandExecuter.ExecuteAsync(
    new SocketCommandContext(Client,Message),
    CommandStartOffset,
    CommandServices
   );
   if(!CommandResult.IsSuccess){
    try{
     await Message.Channel.SendMessageAsync(
      string.Format("{0}: {1}",CommandResult.Error,CommandResult.ErrorReason)
     );
    }catch(HttpException Error){
     Log.Discord_.LogVerbose("Error sending Discord command failure message: "+Error.Message);
     return;
    }catch(HttpRequestException Error){
     Log.Discord_.LogVerbose("Error sending Discord command failure message: "+Error.Message);
     return;
    }
   }
  }
 }
}