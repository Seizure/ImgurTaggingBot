﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Imgur.API.Models;
using Tagaroo.Imgur;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Discord.Commands{
 public class RefreshImgurOAuthTokenCommand : CommandBase{
  private readonly ImgurInterfacer Imgur;
  public RefreshImgurOAuthTokenCommand(ImgurInterfacer Imgur):base(){
   this.Imgur=Imgur;
  }

  [Command("RefreshImgurOAuth")]
  public Task Execute(){
   return Execute(string.Empty);
  }

  [Command("RefreshImgurOAuth")]
  [Summary("Refreshes the Imgur OAuth Token that the application uses to access its account on Imgur. The application will attempt to do this manually, when the Token is close to expiring. It is strongly recommended that the new OAuth Token be backed up; it will be written to and stored in the application's Settings.")]
  public async Task Execute(
   [Remainder] [Summary("Specify the string '"+ConfirmString+"' to confirm execution of this command")]
   string Confirm
  ){
   if( ! ConfirmString.Equals( Confirm, StringComparison.CurrentCultureIgnoreCase )){
    await Usage();
    return;
   }
   IOAuth2Token NewToken;
   try{
    NewToken = await Imgur.RefreshUserAuthenticationToken();
   }catch(ImgurException Error){
    await base.ReplyAsync("Error refreshing Imgur OAuth Token: "+Error.Message);
    return;
   }
   await base.ReplyAsync(string.Format(
    "Imgur OAuth Token refresh successful. The new Token will expire at {0:u}."
    +"\r\nIt is strongly recommended that an administrator backup the new Token."
    ,NewToken.ExpiresAt
   ));
  }

  protected Task Usage(){
   return base.ReplyAsync(
    "This command will refresh the Imgur OAuth Token that is used to access the application's Imgur user account."
    +" Imgur OAuth Tokens expire after a certain length of time, and must be refreshed using the old Token; the application attempts to do this automatically."
    +" The refreshed OAuth Token will be saved in the application's Settings, at which point the old OAuth Token will no longer work."
    +" It is therefore strongly recommended that an administrator consult the application's Settings in order to backup the new OAuth Token, in case it is lost."
    +" If the OAuth Token is lost, a new one will have to be generated by hand, which is not a straightforward process."
    +" \r\n\r\nIf you understand the connotations of this command, enter it again, followed by the string '"+ConfirmString+"'."
   );
  }

  [Command("ImgurOAuthExpiry")]
  [Summary("Displays the expiry date–time of the current Imgur OAuth Token, at which point it ceases to be valid, and must be refreshed. The application will attempt to refresh the OAuth Token automatically, close to this time.")]
  public Task DisplayExpiry(
  ){
   return base.ReplyAsync(string.Format(
    "Imgur OAuth Token expiry date–time — {0:u}",
    Imgur.OAuthTokenExpiry
   ));
  }

  protected const string ConfirmString="confirm";
 }
}