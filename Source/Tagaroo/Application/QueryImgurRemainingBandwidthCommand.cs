using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Imgur.API.Models;
using Tagaroo.Imgur;
using Tagaroo.Logging;

using DiscordCommandBase=Tagaroo.Discord.DiscordCommandBase;
using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Application{
 /// <summary>
 /// Application-layer activity class, executed by the Discord.Commands API; see <see cref="DiscordCommandBase"/>.
 /// Implements a command for displaying the remaining Imgur API bandwidth available to the application.
 /// </summary>
 public class QueryImgurRemainingBandwidthCommand : DiscordCommandBase{
  private readonly ImgurInterfacer Imgur;
  public QueryImgurRemainingBandwidthCommand(ImgurInterfacer Imgur,Discord.MessageSender MessageSender):base(MessageSender){
   this.Imgur=Imgur;
  }

  [Command("APIUsage")]
  [Summary("Displays the remaining number of API calls that the application may make to the Imgur API. This figure is reset periodically, typically every 24 hours.")]
  public async Task Execute(
  ){
   Log.Application_.LogVerbose("Executing APIUsage");
   IRateLimit Result;
   try{
    Result = await Imgur.ReadRemainingBandwidth();
   }catch(ImgurException Error){
    await base.ReplyAsync("Error acquiring remaining API bandwidth from Imgur: "+Error.Message);
    return;
   }
   await base.ReplyAsync(string.Format(
    "Imgur API Remaining Bandwidth (in API calls) — {0:D} / {1:D}",
    Result.ClientRemaining,Result.ClientLimit
   ));
  }
 }
}