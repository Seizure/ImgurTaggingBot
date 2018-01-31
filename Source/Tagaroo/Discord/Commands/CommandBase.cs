using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Discord;
using Discord.Commands;
using Discord.Net;

namespace Tagaroo.Discord.Commands{
 abstract public class CommandBase : ModuleBase<SocketCommandContext>{
  protected CommandBase(){}

  protected override Task<IUserMessage> ReplyAsync(string message,bool isTTS=false,Embed embed=null,RequestOptions options=null){
   if(string.IsNullOrEmpty(message)){
    message="-";
   }
   try{
    return base.ReplyAsync(message,isTTS,embed,options);
   }catch(HttpException){
   }catch(HttpRequestException){
   }
   return Task.FromResult<IUserMessage>(null);
  }
 }
}