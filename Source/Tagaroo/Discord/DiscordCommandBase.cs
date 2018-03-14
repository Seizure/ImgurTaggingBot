using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Discord;
using Discord.Commands;
using Discord.Net;
using Tagaroo.Logging;

namespace Tagaroo.Discord{
 /// <summary>
 /// Serves as a base class for application-layer activity classes that make use of the Discord.Commands API.
 /// Derivations of this class will be detected when <see cref="DiscordInterfacer.Initialize"/> is executed,
 /// and will be called by the Discord.Commands API
 /// when a relevant Discord message is received on one of the command Discord Channels associated with <see cref="DiscordInterfacer"/>.
 /// The Discord.Commands API will inject objects into the constructor parameters of derived classes;
 /// the types that may appear as constructor parameters of derived classes depends on
 /// the <see cref="IServiceProvider"/> provided to <see cref="DiscordInterfacer.Initialize"/>.
 /// </summary>
 abstract public class DiscordCommandBase : ModuleBase<SocketCommandContext>{
  protected DiscordCommandBase(){}

  /// <summary>
  /// Sends a message to the Discord Text Channel from which the current Discord command was given.
  /// Any problems in sending the message are swallowed, and null is returned.
  /// </summary>
  protected override Task<IUserMessage> ReplyAsync(string message,bool isTTS=false,Embed embed=null,RequestOptions options=null){
   if(string.IsNullOrWhiteSpace(message)){
    message="-";
   }
   try{
    return base.ReplyAsync(message,isTTS,embed,options);
   }catch(HttpException Error){
    Log.Discord_.LogVerbose("Error sending Discord command reply message in {0}: {1}",this.GetType().Name,Error.Message);
   }catch(HttpRequestException Error){
    Log.Discord_.LogVerbose("Network error sending Discord command reply message in {0}: {1}",this.GetType().Name,Error.Message);
   }
   return Task.FromResult<IUserMessage>(null);
  }
 }
}