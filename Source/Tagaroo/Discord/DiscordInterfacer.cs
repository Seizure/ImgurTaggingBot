using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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

 /// <summary>
 /// The application's interface to the services provided by Discord.
 /// A <see cref="DiscordException"/> is thrown if there is some problem interacting with Discord;
 /// one may also be thrown in other situations, depending on the method.
 /// </summary>
 public interface DiscordInterfacer{
  /// <summary>
  /// <para>Preconditions: State is Uninitialized</para>
  /// <para>Postconditions: State is Disconnected</para>
  /// This should be called soon after construction of an instance, and before any other methods are called,
  /// to allow the instance to complete construction.
  /// Calls <see cref="CommandChannel.Initialize"/> of all associated <see cref="CommandChannel"/> instances.
  /// </summary>
  /// <param name="CommandServices">
  /// Passed to <see cref="CommandChannel.Initialize"/> of all associated <see cref="CommandChannel"/> instances
  /// </param>
  /// <param name="SynchronizationContext">
  /// The <see cref="SynchronizationContext"/> with which to use to synchronize incoming asynchronous events to,
  /// primarily incoming messages, to ensure thread safety
  /// </param>
  void Initialize(IServiceProvider CommandServices,SynchronizationContext SynchronizationContext);

  /// <summary>
  /// <para>Preconditions: State is not Uninitialized, Connecting, or Disconnecting</para>
  /// <para>Postconditions (method): State is Connecting (provided not already Connected)</para>
  /// <para>Postconditions (task): State is Connected if successful, Disconnected otherwise</para>
  /// Establishes a connection to Discord and the Discord Guild the application is associated with.
  /// Upon establishing a connection to the Guild, several checks are made;
  /// if any fail, an exception is thrown, and the call is not successful regarding Postconditions.
  /// The checks include making sure the Guild is visible/accessible to the application,
  /// that the Channels of all component <see cref="CommandChannel"/> instances exist and are visible within the Guild,
  /// and that the Channel that <see cref="LogMessage"/> uses exists and is visible within the Guild.
  /// The Discord.Net library appears to automatically handle network connection dropouts.
  /// </summary>
  /// <exception cref="DiscordException">Also thrown if any of the checks fail</exception>
  Task Connect();
  
  /// <summary>
  /// <para>Preconditions: None</para>
  /// <para>Postconditions (method): State is Disconnecting (provided not already Disconnected)</para>
  /// <para>Postconditions (task): State is Disconnected</para>
  /// Any problems that occur during the disconnect are silently swallowed.
  /// </summary>
  Task Shutdown();
  
  /// <summary>
  /// See <see cref="TextChannelExists(ulong,out bool)"/>
  /// </summary>
  bool TextChannelExists(ulong channelID);
  /// <summary>
  /// <para>Preconditions: State is Connected</para>
  /// Returns true if there is a Text Channel in the Guild with the supplied ID that is visible to the application, otherwise false.
  /// </summary>
  /// <param name="ChannelName">If the Channel exists, its identifying name within its parent Guild; undefined if the Channel does not exist</param>
  /// <param name="IsNSFW">If the Channel exists, whether or not it is classed as NSFW; undefined if the Channel does not exist</param>
  bool TextChannelExists(ulong channelID,out string ChannelName,out bool IsNSFW);

  /// <summary>
  /// <para>Preconditions: State is Connected</para>
  /// Posts the details of a <see cref="GalleryItem"/> to the specified Channel in the Guild.
  /// </summary>
  /// <exception cref="DiscordException">Also thrown if the specified Text Channel does not exist or is not visible in the Guild</exception>
  Task PostGalleryItemDetails(ulong ChannelID, GalleryItem ToPost);
  
  /// <summary>
  /// <para>Preconditions: <paramref name="Message"/> is not empty, State is Connected</para>
  /// Posts a message to the specified Channel in the Guild.
  /// </summary>
  /// <param name="NSFW">
  /// Set to true if the message is to be regarded as NSFW (this parameter is not currently used)
  /// </param>
  /// <exception cref="DiscordException">Also thrown if the specified Text Channel does not exist or is not visible in the Guild</exception>
  Task SendMessage(ulong ChannelID, string Message, bool NSFW);

  /// <summary>
  /// <para>Preconditions: None</para>
  /// Attempts to post a log message to the designated log Channel of the Guild, returning false on failure;
  /// callers should have an appropriate backup logging strategy in case of failure.
  /// Will always fail if not in a Connected state.
  /// </summary>
  Task<bool> LogMessage(string Message);
 }

 public class DiscordInterfacerMain : DiscordInterfacer{
  private readonly DiscordSocketClient Client;
  private readonly string AuthenticationToken;
  private readonly ulong GuildID;
  private readonly ulong LogChannelID;
  private readonly CommandService CommandExecuter;
  private readonly IReadOnlyDictionary<ulong,CommandChannel> CommandChannelSources;
  private SynchronizationContext SynchronizationContext;
  private States State=States.Disconnected;
  private ulong? Self;
  private SocketGuild Guild=null;
  private ITextChannel LogChannel=null;
  private bool loggingmessage=false;
  
  /// <summary>
  /// <para>Postconditions: State is Uninitialized</para>
  /// </summary>
  /// <param name="AuthenticationToken">
  /// The "Token" portion of the Discord API key with which to connect and authenticate
  /// to the application's associated Discord account with;
  /// note this is not the same as the "Client ID" or "Client Secret" portions of the API key
  /// </param>
  /// <param name="GuildID">
  /// The identifying numeric ID of the Guild (Discord Server) to interact with;
  /// a single instance of this application is only capable of interacting with a single Guild
  /// </param>
  /// <param name="LogChannelID">
  /// The identifying numeric ID of the Discord Channel within the Guild
  /// where <see cref="LogMessage"/> will attempt to send messages to
  /// </param>
  /// <param name="CommandChannelID">
  /// Passed to the single component <see cref="CommandChannel"/> of this class
  /// </param>
  /// <param name="CommandChannelPrefix">
  /// Passed to the single component <see cref="CommandChannel"/> of this class
  /// </param>
  public DiscordInterfacerMain(
   string AuthenticationToken,ulong GuildID,ulong LogChannelID,
   ulong CommandChannelID,string CommandChannelPrefix
  ){
   this.Client = new DiscordSocketClient( new DiscordSocketConfig(){
    WebSocketProvider=WS4NetProvider.Instance,
    HandlerTimeout=null
   });
   if(AuthenticationToken is null){throw new ArgumentNullException(nameof(AuthenticationToken));}
   this.AuthenticationToken=AuthenticationToken;
   this.GuildID=GuildID;
   this.LogChannelID=LogChannelID;
   this.CommandExecuter = new CommandService( new CommandServiceConfig(){
    CaseSensitiveCommands = false,
    ThrowOnError = false,
    LogLevel = LogSeverity.Debug
   });
   this.CommandChannelSources = new Dictionary<ulong,CommandChannel>{
    {CommandChannelID, new CommandChannel(CommandChannelID, this.CommandExecuter, this.Client, CommandChannelPrefix)}
   };
   /*
   Many of these event handlers are not called in a synchronized manner,
   and so may need to be properly synchronized to the current SynchronizationContext
   */
   Client.Log+=onClientLogMessage;
   Client.MessageReceived+=_onMessage;
   Client.Disconnected+=onDisconnected;
   Client.Connected+=onConnected;
   Client.LoggedIn+=onLoggedIn;
   Client.LoggedIn+=onLoggedOut;
   CommandExecuter.Log+=onClientLogMessage;
  }

  public void Initialize(IServiceProvider CommandServices,SynchronizationContext SynchronizationContext){
   if(!(this.SynchronizationContext is null)){throw new InvalidOperationException();}
   if(SynchronizationContext is null){throw new ArgumentNullException(nameof(SynchronizationContext));}
   Log.Bootstrap_.LogVerbose("Initializing CommandChannel instances");
   foreach(CommandChannel InitializeCommandChannel in CommandChannelSources.Values){
    InitializeCommandChannel.Initialize(CommandServices);
   }
   Log.Bootstrap_.LogInfo("Loading Discord commands");
   ICollection<ModuleInfo> LoadedCommands = this.CommandExecuter.AddModulesAsync(
    Assembly.GetExecutingAssembly()
   ).GetAwaiter().GetResult().ToList();
   Log.Bootstrap_.LogVerbose("Discord command modules loaded: {0}",LoadedCommands.Count);
   this.SynchronizationContext=SynchronizationContext;
  }

  public async Task Connect(){
   if(this.SynchronizationContext is null){throw new InvalidOperationException("Not initialized");}
   if(State==States.Connected){
    return;
   }else if(State==States.Connecting||State==States.Disconnecting){
    throw new InvalidOperationException("Cannot connect while connecting/disconnecting");
   }
   TaskCompletionSource<object> BecomingReady=new TaskCompletionSource<object>();
   Func<Task> OnReady=()=>{
    Log.Bootstrap_.LogVerbose("Connecting; now ready");
    BecomingReady.SetResult(null);
    return Task.CompletedTask;
   };
   Client.Ready += OnReady;
   this.State = States.Connecting;
   bool success=false;
   try{
    Log.Bootstrap_.LogVerbose("Connecting; beginning login");
    await Client.LoginAsync(TokenType.Bot,this.AuthenticationToken);
    Log.Bootstrap_.LogVerbose("Connecting; starting session");
    await Client.StartAsync();
    Log.Bootstrap_.LogVerbose("Connecting; waiting until ready");
    await BecomingReady.Task;
    Log.Bootstrap_.LogVerbose("Connecting; connect complete");
    success = true;
   }catch(HttpException Error){
    throw new DiscordException(Error.Message,Error);
   }catch(HttpRequestException Error){
    throw new DiscordException(Error.Message,Error);
   }finally{
    if(!success){
     this.State = States.Disconnected;
    }
   }
   Client.Ready -= OnReady;
   try{
    Log.Bootstrap_.LogVerbose("Connecting; Identifying Guild Channels and performing checks");
    IdentifyChannels(this.GuildID, this.LogChannelID, this.CommandChannelSources.Keys, out this.Guild, out this.LogChannel);
   }catch(DiscordException){
    await Shutdown();
    throw;
   }
   this.Self=Client.CurrentUser.Id;
   this.State = States.Connected;
  }

  /// <exception cref="DiscordException"/>
  private void IdentifyChannels(
   ulong GuildID,ulong LogChannelID,IEnumerable<ulong> CommandChannelsIDs,
   out SocketGuild Guild,out ITextChannel LogChannel
  ){
   //Check the Guild with ID GuildID exists and is visible to the application
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
   //Check the log Channel with ID LogChannelID exists in the Guild and is visible
   string AvailableChannels = string.Join(Environment.NewLine,(
    from C in Guild.TextChannels
    select string.Format("{1} [{0:D}]",C.Id,C.Name)
   ));
   Log.Bootstrap_.LogInfo("Available Channels:"+Environment.NewLine+AvailableChannels);
   LogChannel = Guild.GetTextChannel(LogChannelID);
   if(LogChannel is null){
    throw new DiscordException( string.Format(
     "The Log Channel with ID {0:D} could not be found in the '{2}' [{1:D}] Guild's Text Channels ({3} total), which are:",
     LogChannelID,Guild.Id,Guild.Name,Guild.TextChannels.Count
    ) + Environment.NewLine + AvailableChannels );
   }
   //Check all the command Channels with IDs in CommandChannelsIDs exist in the Guild and are visible
   foreach(ulong CommandChannelID in CommandChannelsIDs){
    ITextChannel CommandChannel = Guild.GetTextChannel(CommandChannelID);
    if(CommandChannel is null){
     throw new DiscordException( string.Format(
      "The Command Channel with ID {0:D} could not be found in the '{2}' [{1:D}] Guild's Text Channels ({3} total), which are:",
      CommandChannelID,Guild.Id,Guild.Name,Guild.TextChannels.Count
     ) + Environment.NewLine + AvailableChannels );
    }
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
   return TextChannelExists(ChannelID,out string _1,out bool _2);
  }
  public bool TextChannelExists(ulong ChannelID,out string Name,out bool IsNSFW){
   if(State!=States.Connected){throw new InvalidOperationException("Not connected");}
   ITextChannel Result = Guild.GetTextChannel(ChannelID);
   Name = Result?.Name ?? null;
   IsNSFW = Result?.IsNsfw ?? false;
   return !(Result is null);
  }

  public Task PostGalleryItemDetails(ulong ChannelID,GalleryItem ToPost){
   if(State!=States.Connected){throw new InvalidOperationException("Not connected");}
   EmbedBuilder MessageEmbed = new EmbedBuilder()
   .WithTimestamp(ToPost.Created);
   if(ToPost.hasTitle){
    MessageEmbed = MessageEmbed.WithTitle(
     ToPost.Title.Length <= EmbedBuilder.MaxTitleLength ? ToPost.Title : ToPost.Title.Substring(0,EmbedBuilder.MaxTitleLength)
    );
   }
   if(!string.IsNullOrWhiteSpace(ToPost.LinkPage) && Uri.IsWellFormedUriString(ToPost.LinkPage,UriKind.Absolute)){
    MessageEmbed = MessageEmbed
    .WithUrl(ToPost.LinkPage);
   }else{
    Log.Discord_.LogWarning(
     "The URI '{1}' for the Gallery item with ID '{0}' is not a valid absolute URI; unable to provide a working link to it in the Archive Channel",
     ToPost.ID,ToPost.LinkPage
    );
   }
   if(!string.IsNullOrWhiteSpace(ToPost.LinkImage) && Uri.IsWellFormedUriString(ToPost.LinkImage,UriKind.Absolute)){
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
   return SendMessage(
    ChannelID,
    ToPost.hasTitle
    ? string.Format("{1}\r\n{0}",ToPost.LinkPage,ToPost.Title)
      //Message to send must not be empty
    : string.Format("<{0}>",ToPost.LinkPage),
    ToPost.NSFW,
    MessageEmbed.Build()
   );
  }

  public Task SendMessage(ulong ChannelID,string Message,bool NSFW){
   return SendMessage(ChannelID,Message,NSFW,null);
  }
  /// <exception cref="DiscordException"/>
  protected async Task SendMessage(ulong ChannelID,string Message,bool NSFW,Embed EmbeddedItem=null){
   if(State!=States.Connected){throw new InvalidOperationException("Not connected");}
   if(string.IsNullOrEmpty(Message)){
    throw new ArgumentNullException(nameof(Message));
   }
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

  public async Task<bool> LogMessage(string Message){
   //State may also be Uninitialized
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

  private Task _onMessage(SocketMessage Message){
   SynchronizationContext.Post(
    (object _) => onMessage(Message),
    null
   );
   return Task.CompletedTask;
  }

  private Task onMessage(SocketMessage Message){
   //Check required to satisfy precondition on CommandChannel.MessageReceived, as well as for general robustness
   if(State!=States.Connected){return Task.CompletedTask;}
   //Ignore messages that have been sent by this application
   if(isSelf(Message.Author.Id)){return Task.CompletedTask;}
   if(CommandChannelSources.TryGetValue(Message.Channel.Id, out CommandChannel CommandSource)){
    SocketUserMessage UserMessage=Message as SocketUserMessage;
    if(!(UserMessage is null)){
     Log.Discord_.LogVerbose("Received message on Command Channel #{0:D} from user '{1}'",Message.Channel.Id,Message.Author?.Username);
     return CommandSource.MessageReceived(UserMessage);
    }else{
     Log.Discord_.LogVerbose("Received non-user message on Command Channel #{0:D} from user '{2}' (#{1:D})",Message.Channel.Id,Message.Author?.Id??0,Message.Author?.Username);
    }
   }
   return Task.CompletedTask;
  }

  private Task onDisconnected(Exception Error){
   if(Error is null){
    Log.Discord_.LogVerbose("Disconnected");
   }else{
    Log.Discord_.LogWarning("Connection dropout: "+Error.Message);
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