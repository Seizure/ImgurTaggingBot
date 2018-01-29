using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Net.Providers.WS4Net;
using Discord.Commands;
using Tagaroo.Model;
using Tagaroo.Logging;

using HttpException=Discord.Net.HttpException;
using HttpRequestException=System.Net.Http.HttpRequestException;

namespace Tagaroo.Discord{

 public interface DiscordInterfacer{
  void Initialize(IServiceProvider CommandServices);

  /// <exception cref="DiscordException"/>
  Task Connect();
  
  Task Shutdown();
  
  bool TextChannelExists(ulong channelID);
  bool TextChannelExists(ulong channelID,out bool IsNSFW);

  /// <exception cref="DiscordException"/>
  Task PostGalleryItemDetails(ulong ChannelID, GalleryItem ToPost);
  
  /// <exception cref="DiscordException"/>
  Task SendMessage(ulong ChannelID, string Message, bool NSFW, Embed EmbeddedItem=null);

  Task<bool> LogMessage(string Message);
 }

 public interface DiscordCommandInterfacer{
  Task<IResult> ExecuteCommand(SocketUserMessage CommandMessage,int CommandStartOffset=0);

  /// <exception cref="DiscordException"/>
  Task SendMessage(ulong ChannelID, string Message, bool NSFW, Embed EmbeddedItem=null);
 }

 public class DiscordInterfacerMain : DiscordInterfacer, DiscordCommandInterfacer{
  private readonly DiscordSocketClient Client;
  private readonly string AuthenticationToken;
  private readonly ulong GuildID;
  private readonly ulong LogChannelID;
  private readonly CommandService CommandExecuter;
  private readonly IReadOnlyDictionary<ulong,CommandChannel> CommandChannelSources;
  private IServiceProvider CommandServices;
  private States State=States.Disconnected;
  private ulong? Self;
  private SocketGuild Guild=null;
  private ITextChannel LogChannel=null;
  private bool loggingmessage=false;
  
  public DiscordInterfacerMain(
   string AuthenticationToken,ulong GuildID,ulong LogChannelID,
   ulong CommandChannelID,string CommandChannelPrefix
  ){
   this.Client=new DiscordSocketClient(new DiscordSocketConfig(){
    WebSocketProvider=WS4NetProvider.Instance,
    HandlerTimeout=null
   });
   this.AuthenticationToken=AuthenticationToken;
   this.GuildID=GuildID;
   this.LogChannelID=LogChannelID;
   this.CommandExecuter=new CommandService(new CommandServiceConfig(){
    CaseSensitiveCommands=false,
    ThrowOnError=false,
    LogLevel=LogSeverity.Debug
   });
   this.CommandChannelSources=new Dictionary<ulong,CommandChannel>{
    {CommandChannelID, new CommandChannel(this, CommandChannelID, CommandChannelPrefix)}
   };
   /*
   Many of these event handlers are not called in a synchronized manner,
   and so may need to be properly synchronized to the current SynchronizationContext
   */
   Client.Log+=onClientLogMessage;
   Client.MessageReceived+=onMessage;
   Client.Disconnected+=onDisconnected;
   Client.Connected+=onConnected;
   Client.LoggedIn+=onLoggedIn;
   Client.LoggedIn+=onLoggedOut;
   CommandExecuter.Log+=onClientLogMessage;
  }

  public void Initialize(IServiceProvider CommandServices){
   if(this.CommandServices!=null){throw new InvalidOperationException();}
   this.CommandServices=CommandServices;
   Log.Bootstrap_.LogInfo("Loading Discord commands");
   ICollection<ModuleInfo> LoadedCommands=CommandExecuter.AddModulesAsync(
    Assembly.GetExecutingAssembly()
   ).Result.ToList();
   Log.Bootstrap_.LogVerbose("Discord command modules loaded: {0}",LoadedCommands.Count);
  }

  public async Task Connect(){
   if(State==States.Connected){
    return;
   }else if(State==States.Connecting||State==States.Disconnecting){
    throw new InvalidOperationException("Cannot connect while connecting/disconnecting");
   }
   this.State = States.Connecting;
   TaskCompletionSource<object> BecomingReady=new TaskCompletionSource<object>();
   Func<Task> OnReady=()=>{
    BecomingReady.SetResult(null);
    return Task.CompletedTask;
   };
   Client.Ready += OnReady;
   try{
    await Client.LoginAsync(TokenType.Bot,this.AuthenticationToken);
    await Client.StartAsync();
   }catch(HttpException Error){
    this.State = States.Disconnected;
    throw new DiscordException(Error.Message,Error);
   }catch(HttpRequestException Error){
    this.State = States.Disconnected;
    throw new DiscordException(Error.Message,Error);
   }
   await BecomingReady.Task;
   Client.Ready -= OnReady;
   this.Self=Client.CurrentUser.Id;
   this.State = States.Connected;
   //Log.Bootstrap_.LogInfo("Successfully connected to Discord");
   IdentifyChannels(this.GuildID, this.LogChannelID, out this.Guild, out this.LogChannel);
  }

  /// <exception cref="DiscordException"/>
  private void IdentifyChannels(
   ulong GuildID,ulong LogChannelID,
   out SocketGuild Guild,out ITextChannel LogChannel
  ){
   string AvailableGuilds = string.Join(Environment.NewLine,(
    from G in this.Client.Guilds
    select string.Format("{1} ({2}) [{0:D}]",G.Id,G.Name,G.Owner?.Username)
   ));
   Log.Bootstrap_.LogInfo("Available Guilds:"+Environment.NewLine+AvailableGuilds);
   Guild = this.Client.GetGuild(GuildID);
   if(Guild is null){
    throw new DiscordException( string.Format(
     "The Guild with ID {0:D} could not be found amongst this application's available Guilds ({1} total), which are:",
     GuildID,Client.Guilds.Count
    ) + Environment.NewLine + AvailableGuilds );
   }
   string AvailableChannels = string.Join(Environment.NewLine,(
    from C in Guild.TextChannels
    select string.Format("{1} [{0:D}]",C.Id,C.Name)
   ));
   Log.Bootstrap_.LogInfo("Available Channels:"+Environment.NewLine+AvailableChannels);
   LogChannel = Guild.GetTextChannel(LogChannelID);
   if(LogChannel is null){
    throw new DiscordException( string.Format(
     "The Channel with ID {0:D} could not be found in the '{2}' [{1:D}] Guild's Text Channels ({3} total), which are:",
     LogChannelID,Guild.Id,Guild.Name,Guild.TextChannels.Count
    ) + Environment.NewLine + AvailableChannels );
   }
  }

  public async Task Shutdown(){
   if(State==States.Disconnected){return;}
   this.State = States.Disconnecting;
   try{
    await Client.StopAsync();
   }catch(HttpException){
   }catch(HttpRequestException){}
   try{
    await Client.LogoutAsync();
   }catch(HttpException){
   }catch(HttpRequestException){}
   this.State = States.Disconnected;
   this.Guild=null;
   this.LogChannel=null;
   this.Self=null;
  }

  protected bool isSelf(ulong DiscordUserID){
   if(State!=States.Connected){throw new InvalidOperationException("Not connected");}
   return DiscordUserID==this.Self;
  }

  public bool TextChannelExists(ulong ChannelID){
   return TextChannelExists(ChannelID,out bool _);
  }
  public bool TextChannelExists(ulong ChannelID,out bool IsNSFW){
   if(State!=States.Connected){throw new InvalidOperationException("Not connected");}
   ITextChannel Result = Guild.GetTextChannel(ChannelID);
   IsNSFW = Result?.IsNsfw ?? false;
   return !(Result is null);
  }

  public async Task PostGalleryItemDetails(ulong ChannelID,GalleryItem ToPost){
   if(State!=States.Connected){throw new InvalidOperationException("Not connected");}
   EmbedBuilder MessageEmbed = new EmbedBuilder()
   .WithTimestamp(ToPost.Created);
   if(ToPost.hasTitle){
    MessageEmbed = MessageEmbed.WithTitle(
     ToPost.Title.Length <= EmbedBuilder.MaxTitleLength ? ToPost.Title : ToPost.Title.Substring(0,EmbedBuilder.MaxTitleLength)
    );
   }
   if(Uri.IsWellFormedUriString(ToPost.LinkPage,UriKind.Absolute)){
    MessageEmbed = MessageEmbed
    .WithUrl(ToPost.LinkPage);
   }else{
    Log.Discord_.LogWarning(
     "The URI '{1}' for the Gallery item with ID '{0}' is not a valid absolute URI; unable to provide a working link to it in the Archive Channel",
     ToPost.ID,ToPost.LinkPage
    );
   }
   if(Uri.IsWellFormedUriString(ToPost.LinkImage,UriKind.Absolute)){
    MessageEmbed = MessageEmbed
    .WithImageUrl(ToPost.LinkImage);
   }
   if(ToPost.hasKnownAuthor){
    MessageEmbed = MessageEmbed.WithAuthor(
     ToPost.AuthorUsername.Length <= EmbedAuthorBuilder.MaxAuthorNameLength ? ToPost.AuthorUsername : ToPost.AuthorUsername.Substring(0,EmbedAuthorBuilder.MaxAuthorNameLength)
    );
   }
   if(ToPost.hasDescription){
    int RemainingSpace = EmbedBuilder.MaxEmbedLength - ((MessageEmbed.Title?.Length??0) + (MessageEmbed.Author?.Name.Length??0));
    MessageEmbed = MessageEmbed.WithDescription(
     ToPost.Description.Length <= RemainingSpace ? ToPost.Description : ToPost.Description.Substring(0,RemainingSpace)
    );
   }
   await SendMessage(
    ChannelID,
    ToPost.hasTitle
    ? string.Format("{1}\r\n{0}",ToPost.LinkPage,ToPost.Title)
    : string.Format("{0}",ToPost.LinkPage),
    ToPost.NSFW,
    MessageEmbed.Build()
   );
  }

  public async Task SendMessage(ulong ChannelID,string Message,bool NSFW,Embed EmbeddedItem=null){
   if(State!=States.Connected){throw new InvalidOperationException("Not connected");}
   ITextChannel Channel = Guild.GetTextChannel(ChannelID);
   if(Channel is null){
    throw new DiscordException(string.Format(
     "The Channel with ID {0:D} does not exist in the Guild '{2}' [{1:D}]",
     ChannelID,Guild.Id,Guild.Name
    ));
   }
   /*
   if(NSFW && !Channel.IsNsfw){
    throw new DiscordException(string.Format(
     "The message to send was marked as NSFW, but the Channel it was being sent to, '{0}', is not NSFW",
     Channel.Name
    ));
   }
   */
   try{
    await Channel.SendMessageAsync(Message, embed:EmbeddedItem);
   }catch(HttpException Error){
    throw new DiscordException(Error.Message,Error);
   }catch(HttpRequestException Error){
    throw new DiscordException(Error.Message,Error);
   }
  }

  public async Task<IResult> ExecuteCommand(SocketUserMessage CommandMessage,int CommandStartOffset=0){
   return await CommandExecuter.ExecuteAsync(
    new SocketCommandContext(Client,CommandMessage),
    CommandStartOffset,
    CommandServices
   );
  }

  public async Task<bool> LogMessage(string Message){
   if(
    State!=States.Connected
    ||Client.ConnectionState!=ConnectionState.Connected
    ||Client.LoginState!=LoginState.LoggedIn
   ){
    return false;
   }
   //Prevent logging of any messages whilst logging a message, re-entrancy, which may cause infinite recursion
   if(loggingmessage){return false;}
   this.loggingmessage = true;
   try{
    await LogChannel.SendMessageAsync(Message);
    return true;
   }catch(HttpException){
   }catch(HttpRequestException){
   }finally{
    this.loggingmessage = false;
   }
   return false;
  }

  private Task onMessage(SocketMessage Message){
   //TODO Check if calls to this method are properly synchronized
   if(State!=States.Connected){return Task.CompletedTask;}
   if(isSelf(Message.Author.Id)){return Task.CompletedTask;}
   /*
   TODO Discord Commands:
   Shutdown
   Reload Settings
   Manual OAuth Token refresh
   User ID lookup
   Query Rate remaining
   Configure Settings
   Query OAuth Token expiry, also log message on imminent expiry
   */
   if(CommandChannelSources.TryGetValue(Message.Channel.Id, out CommandChannel CommandSource)){
    SocketUserMessage UserMessage=Message as SocketUserMessage;
    if(UserMessage!=null){
     return CommandSource.MessageReceived(UserMessage);
    }
   }
   return Task.CompletedTask;
  }

  private Task onDisconnected(Exception Error){
   if(Error!=null){
    Log.Discord_.LogWarning("Connection dropout: "+Error.Message);
   }else{
    Log.Discord_.LogVerbose("Disconnected");
   }
   return Task.CompletedTask;
  }
  private Task onConnected(){
   Log.Discord_.LogVerbose("Connected");
   return Task.CompletedTask;
  }
  private Task onLoggedIn(){
   Log.Discord_.LogVerbose("Logged in");
   return Task.CompletedTask;
  }
  private Task onLoggedOut(){
   Log.Discord_.LogVerbose("Logged out");
   return Task.CompletedTask;
  }

  //Calls made by Discord.Net to this event handler method may not be synchronized to the current SynchronizationContext
  private Task onClientLogMessage(LogMessage MessageDetail){
   string Message;
   if(MessageDetail.Exception is null){
    Message = string.Format("{0} :: {1}", MessageDetail.Source, MessageDetail.Message);
   }else if(MessageDetail.Message is null){
    Message = string.Format("{0} :: {1}", MessageDetail.Source, MessageDetail.Exception.Message);
   }else{
    Message = string.Format("{0} :: {1}: {2}", MessageDetail.Source, MessageDetail.Message, MessageDetail.Exception.Message);
   }
   switch(MessageDetail.Severity){
    case LogSeverity.Critical: Log.Instance.DiscordLibrary.LogCritical(Message); break;
    case LogSeverity.Error:    Log.Instance.DiscordLibrary.LogError(Message); break;
    case LogSeverity.Warning:  Log.Instance.DiscordLibrary.LogWarning(Message); break;
    case LogSeverity.Info:     Log.Instance.DiscordLibrary.LogInfo(Message); break;
    case LogSeverity.Verbose:  Log.Instance.DiscordLibrary.LogVerbose(Message); break;
    case LogSeverity.Debug:    Log.Instance.DiscordLibrary.LogVerbose(Message); break;
   }
   return Task.CompletedTask;
  }

  protected enum States{Disconnected,Connecting,Connected,Disconnecting}
 }
}