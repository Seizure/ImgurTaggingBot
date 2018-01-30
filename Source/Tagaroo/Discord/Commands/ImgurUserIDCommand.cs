using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Imgur.API.Models;
using Tagaroo.Imgur;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Discord.Commands{
 public class ImgurUserIDCommand : CommandBase{
  private readonly ImgurInterfacer Imgur;
  public ImgurUserIDCommand(ImgurInterfacer Imgur):base(){
   this.Imgur=Imgur;
  }

  [Command("ToImgurUserID")]
  [Summary("Displays the unique numeric Imgur account ID of the Imgur account with the specified username.")]
  public async Task Execute(
   [Remainder] [Summary("The Imgur account username for which to retrieve the ID for.")]
   string Username
  ){
   IAccount Result;
   try{
    Result = await Imgur.ReadUserDetails(Username);
   }catch(ImgurException Error){
    await base.ReplyAsync(
     string.Format("Error retrieving details for Imgur username '{0}': ",Username)
     +Error.Message
    );
    return;
   }
   await Reply(Result);
  }

  [Command("FromImgurUserID")]
  [Summary("Displays the username of the Imgur account with the specified numeric account ID.")]
  public async Task Execute(
   [Remainder] [Summary("The numeric Imgur account ID for which to retrieve the username for.")]
   int UserID
  ){
   IAccount Result;
   try{
    Result = await Imgur.ReadUserDetails(UserID);
   }catch(ImgurException Error){
    await base.ReplyAsync(
     string.Format("Error retrieving details for Imgur user ID '{0:D}': ",UserID)
     +Error.Message
    );
    return;
   }
   await Reply(Result);
  }

  protected Task Reply(IAccount Details){
   StringBuilder Response=new StringBuilder();
   Response.Append("Imgur Account Details:");
   Response.AppendFormat("\r\nUsername - {0}",Details.Url);
   Response.AppendFormat("\r\nUser ID - {0:D}",Details.Id);
   return base.ReplyAsync(Response.ToString());
  }
 }
}