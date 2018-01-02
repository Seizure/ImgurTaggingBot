using System;

namespace Tagaroo.Discord{
 public class DiscordException:Exception{
  public DiscordException(string Message)
  :base(Message){}
  public DiscordException(string Message,Exception Inner)
  :base(Message,Inner){}
 }
}