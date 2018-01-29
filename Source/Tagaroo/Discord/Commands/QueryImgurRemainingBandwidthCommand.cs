using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Imgur.API.Models;
using Tagaroo.Imgur;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Discord.Commands{
 public class QueryImgurRemainingBandwidthCommand : CommandBase{
  private readonly ImgurInterfacer Imgur;
  public QueryImgurRemainingBandwidthCommand(ImgurInterfacer Imgur):base(){
   this.Imgur=Imgur;
  }

  [Command("APIUsage")]
  [Summary("Displays the remaining number of API calls that the application may make to the Imgur API. This figure is reset periodically, typically every 24 hours.")]
  public async Task Execute(
  ){
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