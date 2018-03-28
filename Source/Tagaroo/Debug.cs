#if DEBUG
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Discord.Net.Providers.WS4Net;
using Imgur.API;
using Imgur.API.Authentication;
using AuthenticationImpl = Imgur.API.Authentication.Impl;
using Imgur.API.Models;
using ModelsImpl = Imgur.API.Models.Impl;
using Imgur.API.Enums;
using ImgurEndpoints = Imgur.API.Endpoints.Impl;
using Tagaroo.Application;
using Tagaroo.Model;
using Tagaroo.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

/*
Dumping ground for testing out stuff
*/

namespace Tagaroo{
 class Debug:Imgur.ImgurCommandHandler{
  private DiscordSocketClient ClientDiscord;

  public async Task RunDiscord(){
   this.ClientDiscord=new DiscordSocketClient(
    new DiscordSocketConfig(){
     WebSocketProvider=WS4NetProvider.Instance
    }
   );
   ClientDiscord.Log+=onLog;
   ClientDiscord.MessageReceived+=onMessage;
   Console.Write("Authentication Token > ");
   string DiscordAuthorizationToken=Console.ReadLine();
   Console.WriteLine("Logging in...");
   await ClientDiscord.LoginAsync(TokenType.Bot,DiscordAuthorizationToken);
   Console.WriteLine("Logged in. Press any key to continue . . .");
   Console.ReadKey(true);
   Console.WriteLine("Starting...");
   await ClientDiscord.StartAsync();
   Console.WriteLine("Started. Press any key to exit");
   Console.ReadKey(true);
   Console.WriteLine("Stopping...");
   await ClientDiscord.StopAsync();
   Console.WriteLine("Logging out...");
   await ClientDiscord.LogoutAsync();
  }

  private async Task onMessage(SocketMessage Message){
   if(Message.Content.StartsWith("/")) {
    StringBuilder Response=new StringBuilder();
    Response.Append("Bot User Details:\n");
    Response.AppendFormat("Username — {0}\n",ClientDiscord.CurrentUser.Username);
    Response.AppendFormat("ID — {0}\n",ClientDiscord.CurrentUser.Id);
    Response.AppendFormat("Avatar ID — {0}\n",ClientDiscord.CurrentUser.AvatarId);
    Response.AppendFormat("Status — {0}\n",ClientDiscord.CurrentUser.Status);
    Response.AppendFormat("Created — {0}\n",ClientDiscord.CurrentUser.CreatedAt);
    Response.AppendFormat("E-Mail — {0}\n",ClientDiscord.CurrentUser.Email);
    Response.AppendFormat("Verified — {0}\n",ClientDiscord.CurrentUser.IsVerified);
    Response.AppendFormat("Verified ?MFA — {0}\n",ClientDiscord.CurrentUser.IsMfaEnabled);
    Response.AppendFormat("?Mention — {0}\n",ClientDiscord.CurrentUser.Mention);
    Response.AppendFormat("?Discriminator — '{0}' = {1}\n",ClientDiscord.CurrentUser.Discriminator,ClientDiscord.CurrentUser.DiscriminatorValue);
    Response.Append("\nBot Environment Details:\n");
    Response.AppendFormat("?Shard ID — {0}\n",ClientDiscord.ShardId);
    Response.AppendFormat("Estimated connection latency, ms — {0}\n",ClientDiscord.Latency);
    Response.AppendFormat("Total DM Channels — {0}\n",ClientDiscord.DMChannels.Count);
    Response.AppendFormat("Total Group Channels — {0}\n",ClientDiscord.GroupChannels.Count);
    Response.AppendFormat("Total Private Channels — {0}\n",ClientDiscord.PrivateChannels.Count);
    Response.AppendFormat("Total ?Guilds — {0}\n",ClientDiscord.Guilds.Count);
    Response.AppendFormat("Total Voice Regions — {0}\n",ClientDiscord.VoiceRegions.Count);
    Response.Append("\nReceived Message Details:\n");
    Response.AppendFormat("ID — {0}\n",Message.Id);
    Response.AppendFormat("Created — {0}\n",Message.CreatedAt);
    Response.AppendFormat("Timestamp — {0}\n",Message.Timestamp);
    Response.AppendFormat("Pinned — {0}\n",Message.IsPinned);
    Response.AppendFormat("?TTS — {0}\n",Message.IsTTS);
    Response.AppendFormat("Total Attachments — {0}\n",Message.Attachments.Count);
    Response.AppendFormat("Total ?Embeds — {0}\n",Message.Embeds.Count);
    Response.AppendFormat("Total ?Tags — {0}\n",Message.Tags.Count);
    Response.AppendFormat("Total Mentioned Users — {0}\n",Message.MentionedUsers.Count);
    Response.AppendFormat("Total Mentioned Channels — {0}\n",Message.MentionedChannels.Count);
    Response.AppendFormat("Total Mentioned ?Roles — {0}\n",Message.MentionedRoles.Count);
    Response.AppendFormat("Author Username — {0}\n",Message.Author.Username);
    Response.AppendFormat("Author ID — {0}\n",Message.Author.Id);
    Response.AppendFormat("Author Type — {0}\n",Message.Source);
    Response.AppendFormat("Author Avatar ID — {0}\n",Message.Author.AvatarId);
    Response.AppendFormat("Author Status — {0}\n",Message.Author.Status);
    Response.AppendFormat("Author Created — {0}\n",Message.Author.CreatedAt);
    Response.AppendFormat("Channel Name — {0}\n",Message.Channel.Name);
    Response.AppendFormat("Channel ID — {0}\n",Message.Channel.Id);
    Response.AppendFormat("Channel Created — {0}\n",Message.Channel.CreatedAt);
    Response.AppendFormat("Channel NSFW — {0}\n",Message.Channel.IsNsfw?"You are in an NSFW channel, you naughty thing!":(object)false);
    Response.AppendFormat("Channel ?Cached Message Total — {0}\n",Message.Channel.CachedMessages.Count);
    RestUserMessage SentMessage=await Message.Channel.SendMessageAsync(Response.ToString());
   }
  }

  private Task onLog(LogMessage Message){
   Console.WriteLine("{0} {1}: {2}",Message.Source,Message.Severity,Message.Exception!=null?Message.Exception.Message:Message.Message);
   return Task.CompletedTask;
  }

  const string ImgurUser="wereleven";
  const int ImgurUserID=77530931;

  public async Task RunImgUR(){
   Console.Write("Authentication ID > ");
   string ImgurAuthenticationID=Console.ReadLine();
   /*
   Console.Write("Authentication Secret > ");
   string ImgurAuthenticationSecret=Console.ReadLine();
   Console.Write("OAuth Access Token > ");
   string OAuthAccessToken=Console.ReadLine();
   */
   IApiClient ClientImgur=new AuthenticationImpl.ImgurClient(ImgurAuthenticationID);
   //IApiClient ClientImgurAuthenticated=new AuthenticationImpl.ImgurClient(ImgurAuthenticationID,ImgurAuthenticationSecret,new ModelsImpl.OAuth2Token(OAuthAccessToken,string.Empty,"bearer","77530931","wereleven",315360000));
   Imgur.LibraryEnhancements.AccountEndpointEnhanced AccountAPI =new Imgur.LibraryEnhancements.AccountEndpointEnhanced(ClientImgur);
   ImgurEndpoints.ImageEndpoint ImageAPI=new ImgurEndpoints.ImageEndpoint(ClientImgur);
   ImgurEndpoints.AlbumEndpoint AlbumAPI=new ImgurEndpoints.AlbumEndpoint(ClientImgur);
   //ImgurEndpoints.CommentEndpoint CommentAPIAuthenticated=new ImgurEndpoints.CommentEndpoint(ClientImgurAuthenticated);
   //ImgurEndpoints.OAuth2Endpoint OAuthAPI=new ImgurEndpoints.OAuth2Endpoint(ClientImgurAuthenticated);
   ImgurEndpoints.RateLimitEndpoint LimitAPI=new ImgurEndpoints.RateLimitEndpoint(ClientImgur);
   try{
    /*
    Kewl Cat Album ID: YYL69
    Kewl Cat Image ID: 2rzgptw
    Galcian Image ID: BWeb9EM
    */
    Console.WriteLine("Initial connection...");
    IRateLimit RemainingUsage=await LimitAPI.GetRateLimitAsync();
    Console.WriteLine("Remaining API usage - {0} / {1}",RemainingUsage.ClientRemaining,RemainingUsage.ClientLimit);
    Console.WriteLine();
    /*
    Console.WriteLine("Refreshing OAuth Token...");
    Task<IOAuth2Token> wait=OAuthAPI.GetTokenByRefreshTokenAsync(ClientImgurAuthenticated.OAuth2Token.RefreshToken);
    IOAuth2Token RefreshedOAuthToken=await wait;
    ClientImgurAuthenticated.SetOAuth2Token(RefreshedOAuthToken);
    Console.WriteLine("ImgUR Account: {0} [{1}]",RefreshedOAuthToken.AccountUsername,RefreshedOAuthToken.AccountId);
    Console.WriteLine("Type: {0}",RefreshedOAuthToken.TokenType);
    Console.WriteLine("Token: {0}",RefreshedOAuthToken.AccessToken);
    Console.WriteLine("Refresh Token: {0}",RefreshedOAuthToken.RefreshToken);
    Console.WriteLine("Expires: {0} ({1} seconds)",RefreshedOAuthToken.ExpiresAt,RefreshedOAuthToken.ExpiresIn);
    Console.WriteLine();
    */
    Console.WriteLine("Retrieving Account details...");
    //IAccount Account=await AccountAPI.GetAccountAsync(ImgurUser);
    IAccount Account=await AccountAPI.GetAccountAsync(ImgurUserID);
    Console.WriteLine("ID - {0}",Account.Id);
    Console.WriteLine("Username - {0}",Account.Url);
    Console.WriteLine("Created - {0}",Account.Created);
    Console.WriteLine("Notoriety - {0}",Account.Notoriety);
    Console.WriteLine("Reputation - {0}",Account.Reputation);
    Console.WriteLine("Bio - {0}",Account.Bio);
    Console.WriteLine();
    Console.WriteLine("Retrieving recent Comments...");
    IList<IComment> Comments=(await AccountAPI.GetCommentsAsync(ImgurUser,CommentSortOrder.Newest,0)).ToList();
    byte count=0;
    foreach(IComment Comment in Comments){
     if(++count>3){break;}
     DisplayComment(Comment);
    }
    Console.WriteLine("Retrieving recent Comment IDs...");
    IList<int> CommentIDs=(await AccountAPI.GetCommentIdsAsync(ImgurUser,CommentSortOrder.Newest,0)).ToList();
    if(CommentIDs.Count>0){
     Console.WriteLine("Recent Comments - "+string.Join(", ",CommentIDs));
     Console.WriteLine();
     Console.WriteLine("Retrieving most recent Comment ({0})...",CommentIDs[0]);
     IComment Comment=await AccountAPI.GetCommentAsync(CommentIDs[0],ImgurUser);
     DisplayComment(Comment);
     string CommentImageID;
     if(!Comment.OnAlbum){
      CommentImageID=Comment.ImageId;
     }else{
      Console.WriteLine("Retrieving most recent Comment Album details ({0})...",Comment.ImageId);
      IAlbum Album=await AlbumAPI.GetAlbumAsync(Comment.ImageId);
      Console.WriteLine("ID - {0}",Album.Id);
      Console.WriteLine("Title - {0}",Album.Title);
      Console.WriteLine("URL - {0}",Album.Link);
      Console.WriteLine("Owner Username if Album not anonymous - {0}",Album.AccountUrl);
      Console.WriteLine("Cover Image ID - {0}",Album.Cover);
      Console.WriteLine("Cover Image Dimensions - {0}x{1}",Album.CoverWidth,Album.CoverHeight);
      Console.WriteLine("Total Images - {0}",Album.ImagesCount);
      Console.WriteLine("In Gallery - {0}",Album.InGallery);
      Console.WriteLine("Created - {0}",Album.DateTime);
      Console.WriteLine("Views - {0}",Album.Views);
      Console.WriteLine("Category - {0}",Album.Section);
      Console.WriteLine("NSFW - {0}",Album.Nsfw);
      Console.WriteLine("View Layout - {0}",Album.Layout);
      Console.WriteLine("Description - {0}",Album.Description);
      Console.WriteLine();
      CommentImageID=Comment.AlbumCover;
     }
     Console.WriteLine("Retrieving most recent Comment Image details ({0})...",CommentImageID);
     IImage Image=await ImageAPI.GetImageAsync(CommentImageID);
     Console.WriteLine("ID - {0}",Image.Id);
     Console.WriteLine("Title - {0}",Image.Title);
     Console.WriteLine("URL - {0}",Image.Link);
     Console.WriteLine("Filename - {0}",Image.Name);
     Console.WriteLine("MIME - {0}",Image.Type);
     Console.WriteLine("Size - {0}B",Image.Size);
     Console.WriteLine("Dimensions - {0}x{1}",Image.Width,Image.Height);
     Console.WriteLine("Uploaded - {0}",Image.DateTime);
     Console.WriteLine("Views - {0}",Image.Views);
     Console.WriteLine("Category - {0}",Image.Section);
     Console.WriteLine("NSFW - {0}",Image.Nsfw);
     Console.WriteLine("Animated - {0}",Image.Animated);
     Console.WriteLine("Description - {0}",Image.Description);
     Console.WriteLine();
     /*
     Console.Write("Enter Comment to post, or blank to skip > ");
     string CommentReply=Console.ReadLine();
     if(!string.IsNullOrWhiteSpace(CommentReply)){
      int ReplyID=await CommentAPIAuthenticated.CreateReplyAsync(CommentReply,Comment.ImageId,Comment.Id.ToString("D"));
      Console.WriteLine("Created Comment ID - {0}",ReplyID);
     }
     Console.WriteLine();
     */
    }else{
     Console.WriteLine();
    }
   }catch(ImgurException Error){
    Console.Error.WriteLine("Error: "+Error.Message);
   }
   Console.WriteLine("Remaining API usage - {0} / {1}",ClientImgur.RateLimit.ClientRemaining,ClientImgur.RateLimit.ClientLimit);
   Console.ReadKey(true);
  }
  private void DisplayComment(IComment Comment){
   Console.WriteLine("ID - {0}",Comment.Id);
   Console.WriteLine("Parent - {0}",Comment.ParentId);
   Console.WriteLine("Author Username - {0}",Comment.Author);
   Console.WriteLine("Created - {0}",Comment.DateTime);
   Console.WriteLine("Points - {0}",Comment.Points);
   Console.WriteLine("Origin Platform - {0}",Comment.Platform);
   Console.WriteLine("Image/Album ID - {0}",Comment.ImageId);
   Console.WriteLine("Album Comment - {0}",Comment.OnAlbum);
   Console.WriteLine("Album Cover Image ID - {0}",Comment.AlbumCover);
   Console.WriteLine("Deleted - {0}",Comment.Deleted);
   Console.WriteLine("Sub-Comments - {0}",Comment.Children.Count());
   Console.WriteLine("Content - {0}",Comment.CommentText);
   Console.WriteLine();
  }

  public async Task RunDebugImgUR(){
   Console.Write("Authentication ID > ");
   string ImgurAuthenticationID=Console.ReadLine();
   string ImgurAuthenticationSecret="Q",OAuthAccessToken="Q";
   /*
   Console.Write("Authentication Secret > ");
   ImgurAuthenticationSecret=Console.ReadLine();
   Console.Write("OAuth Access Token > ");
   OAuthAccessToken=Console.ReadLine();
   */
   Imgur.ImgurInterfacer ClientImgur=new Imgur.ImgurInterfacerMain(
    new DataAccess.SettingsRepositoryMain(string.Empty),
    ImgurAuthenticationID,ImgurAuthenticationSecret,"Q",3,OAuthAccessToken,"Q","bearer",DateTimeOffset.MaxValue,TimeSpan.FromSeconds(11),0.1F,140,"#"
   );
   IDictionary<string,IList<IComment>> Results=await ClientImgur.ReadCommentsSince(DateTimeOffset.UtcNow.AddMonths(-1),new HashSet<string>(){"TruFox"},10);
   foreach(IComment Result in Results.Values.First()){
    Console.WriteLine("On {0} at {1}:",Result.ImageId,Result.DateTime);
    Console.WriteLine(Result.CommentText);
    Console.WriteLine("Replies - {0}",Result.Children.Count());
    Console.WriteLine();
   }
  }

  public void RunDebugDiscord(){
   Console.Write("Authentication Token > ");
   string DiscordAuthorizationToken=Console.ReadLine();
   Discord.DiscordInterfacer Discord=new Discord.DiscordInterfacerMain(DiscordAuthorizationToken,388542416225042435UL,388542416225042439UL,388542416225042439UL,"/");
   SingleThreadSynchronizationContext RunOn=new SingleThreadSynchronizationContext();
   Discord.Initialize(new ServiceCollection().AddSingleton<Discord.DiscordInterfacer>(Discord).AddSingleton<Imgur.ImgurInterfacer>(new TestImgurInterfacer()).BuildServiceProvider(),RunOn);
   //Logging.Log.Instance.AddTraceListener(new Logging.DiscordTraceListener("DiscordListener",Discord,new System.Diagnostics.TextWriterTraceListener(Console.Out)));
   Logging.Log.Instance.DiscordLevel.Level=System.Diagnostics.SourceLevels.Verbose;
   Logging.Log.Instance.DiscordLibraryLevel.Level=System.Diagnostics.SourceLevels.Warning;
   RunOn.RunOnCurrentThread(async()=>{
    await Discord.Connect();
    /*
    await Task.Delay(1000);
    await Discord.PostGalleryItemDetails(388542416225042439UL,new GalleryItem(
     "Q","Gallery Link","https://imgur.com/gallery/YYL69",null,null,false,DateTimeOffset.UtcNow,string.Empty,null,null
    ));
    await Discord.PostGalleryItemDetails(388542416225042439UL,new GalleryItem(
     "Q","Album Link","https://imgur.com/a/YYL69",null,null,false,DateTimeOffset.UtcNow,string.Empty,null,null
    ));
    await Discord.PostGalleryItemDetails(388542416225042439UL,new GalleryItem(
     "Q","Image Link","https://imgur.com/2rzgptw",null,null,false,DateTimeOffset.UtcNow,string.Empty,null,null
    ));
    await Discord.PostGalleryItemDetails(388542416225042439UL,new GalleryItem(
     "Q","Resource Link","https://i.imgur.com/2rzgptw.jpg",null,null,false,DateTimeOffset.UtcNow,string.Empty,null,null
    ));
    */
    await Task.Run(()=>{
     Console.ReadKey(true);
    });
    await Discord.Shutdown();
    RunOn.Finish();
   });
  }

  public async Task RunDebug(){
   /*
   Imgur.ImgurCommandParser Parser=new Imgur.ImgurCommandParser();
   Parser.ProcessCommands(new ModelsImpl.Comment(){
    CommentText="~@Tagaroo tag taglist-name s category1 category2 category3.@Tagaroo tag\n",
    Id=0
   },this);
   */
   /*
   ICollection<string> Results=Imgur.ImgurInterfacer.ChunkConcatenate(
    new List<string>(){"22","1","4444","666666","7777777","333","22","55555"},
    string.Empty," ",6
   );
   foreach(string Result in Results){
    Console.WriteLine(Result);
    Console.WriteLine();
   }
   */
   /*
   IReadOnlyDictionary<string,Taglist> Results=await new DataAccess.TaglistRepository(@"DataAccess\Taglists.xml",false).LoadAll();
   foreach(Taglist Result in Results.Values){
    Console.WriteLine("Taglist: {0}",Result.Name);
    foreach(TaglistRegisteredUser User in Result.RegisteredUsers){
     Console.WriteLine("{0}; Ratings - {1}; Category blacklist - {2}",User.Username,User.AcceptedRatings,string.Join(", ",User.CategoryBlacklist));
    }
    Console.WriteLine();
   }
   */
   /*
   ICollection<string> Results=await new CoreProcess(
    null,new DataAccess.TaglistRepository(@"DataAccess\Taglists.xml",true),new Settings()
   ).ProcessTagCommand(new Tag(
    0,string.Empty,"TheTaglist",Ratings.Safe,new List<string>(){"A"}
   ));
   foreach(string Result in Results){
    Console.WriteLine(Result);
   }
   */
   /*
   DataAccess.SettingsRepository Repository=new DataAccess.SettingsRepository(@"DataAccess\Settings.xml");
   Settings Model=await Repository.LoadSettings();
   await Repository.SaveNewImgurUserAuthorizationToken("A","R");
   Model.CommenterUsernames.Add("Added");
   Model.PullCommentsFrequency=TimeSpan.FromSeconds(172799);
   Model.RequestThreshholdPullCommentsPerUser=byte.MinValue;
   Model.CommentsProcessedUpToInclusive=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(1));
   await Repository.SaveWritableSettings(Model);
   */
   /*
   DataAccess.SettingsRepository RepositorySettings=new DataAccess.SettingsRepositoryMain(@"Settings.xml");
   DataAccess.TaglistRepository RepositoryTaglist=new DataAccess.TaglistRepositoryMain(@"Taglists.xml",true);
   Settings ModelSettings=await RepositorySettings.LoadSettings();
   var ModelTaglists=await RepositoryTaglist.LoadAll();
   ApplicationConfiguration ModelConfiguration=await RepositorySettings.LoadConfiguration();
   */
   DataAccess.TaglistRepository RepositoryTaglist=new DataAccess.TaglistRepositoryMain(@"Taglists.xml");
   RepositoryTaglist.Initialize();
   var ModelAndLock=await RepositoryTaglist.LoadAndLock("SampleTaglist");
   Taglist Model=ModelAndLock.Item1;
   DataAccess.Lock Lock=ModelAndLock.Item2;
   Model.UnRegisterUser("All");
   Model.UnRegisterUser(56);
   Model.RegisterUser(new TaglistRegisteredUser("None",56,TaglistRegisteredUser.RatingFlags.None,new List<string>(0)));
   Model.RegisterUser(new TaglistRegisteredUser("New Many",255,TaglistRegisteredUser.RatingFlags.Safe|TaglistRegisteredUser.RatingFlags.Questionable|TaglistRegisteredUser.RatingFlags.Explicit,new List<string>{"Category A","Category B","Category C"}));
   try{
    Model.RegisterUser(new TaglistRegisteredUser("Explicit",257,TaglistRegisteredUser.RatingFlags.Explicit,new List<string>(0)));
   }catch(AlreadyExistsException){
   }
   Model.RegisterUser(new TaglistRegisteredUser("Safe",59,TaglistRegisteredUser.RatingFlags.Safe,new List<string>{"NotSafe"}));
   await RepositoryTaglist.Save(Model,Lock);
  }
  
  public void RunDebugSynchronized(){
   var SynchronizationContext=new SingleThreadSynchronizationContext();
   SynchronizationContext.RunOnCurrentThread(async() => {
    Infrastructure.TaskScheduler Scheduler=new Infrastructure.TaskScheduler();
    Scheduler.AddTask(ScheduledTask.NewLaterTask(
     TimeSpan.FromSeconds(2),
     () => {
      Console.WriteLine("Two seconds");
      return Task.CompletedTask;
     }
    ));
    Scheduler.AddTask(ScheduledTask.NewImmediateTask(
     TimeSpan.FromSeconds(6),
     () => {
      Console.WriteLine("Six seconds");
      return Task.CompletedTask;
     }
    ));
    Scheduler.AddTask(ScheduledTask.NewLaterTask(
     TimeSpan.FromSeconds(30),
     () => {
      Console.WriteLine("Shutdown");
      Scheduler.Stop();
      return Task.CompletedTask;
     }
    ));
    await Scheduler.Run();
    SynchronizationContext.Finish();
   });
  }

  Task Imgur.ImgurCommandHandler.ProcessTagCommand(Imgur.TagCommandParameters Parsed){
   Console.WriteLine("Taglist - {0}",Parsed.TaglistName);
   Console.WriteLine("Rating - {0}",Parsed.Rating);
   Console.Write("Categories -");
   foreach(string Category in Parsed.Categories){
    Console.Write(" {0}",Category);
   }
   Console.WriteLine();
   Console.WriteLine();
   return Task.CompletedTask;
  }

  public void RunCore(){
   Logging.Log.Instance.DiscordLibraryLevel.Level=System.Diagnostics.SourceLevels.Warning;
   Console.Write("Discord Authentication Token > ");
   string DiscordAuthorizationToken=Console.ReadLine();
   string ImgurAuthenticationSecret="Q",OAuthAccessToken="Q",OAuthRefreshToken="Q";
   Console.Write("Imgur Authentication ID > ");
   string ImgurAuthenticationID=Console.ReadLine();
   Console.Write("Imgur Authentication Secret > ");
   ImgurAuthenticationSecret=Console.ReadLine();
   Console.Write("Imgur OAuth Access Token > ");
   OAuthAccessToken=Console.ReadLine();
   /*
   Console.Write("Imgur OAuth Refresh Token > ");
   OAuthRefreshToken=Console.ReadLine();
   */
   Discord.DiscordInterfacerMain _Discord;
   Imgur.ImgurInterfacer _Imgur;
   Application.CacheingTaglistRepository RepositoryTaglists;
   Program Core=new Program(
    new ProcessLatestCommentsActivity(
     _Imgur=new Imgur.ImgurInterfacerMain(
      new DataAccess.SettingsRepositoryMain(@"DataAccess\Settings1.xml"),
      ImgurAuthenticationID,ImgurAuthenticationSecret,
      "wereleven",77530931,
      OAuthAccessToken,OAuthRefreshToken,"bearer",
      DateTimeOffset.UtcNow+TimeSpan.FromDays(11),
      TimeSpan.FromSeconds(11),0.1F,140,"#"
     ),
     new DataAccess.SettingsRepositoryMain(@"DataAccess\Settings1.xml"),
     RepositoryTaglists=new CacheingTaglistRepository(new DataAccess.TaglistRepositoryMain(@"DataAccess\Taglists.xml")),
     new ProcessCommentActivity(
      new Imgur.ImgurCommandParser("@Tagaroo2",_Imgur),
      new ProcessTagCommandActivity(
       _Imgur,
       _Discord=new Discord.DiscordInterfacerMain(
        DiscordAuthorizationToken,
        388542416225042435UL,388542416225042439UL,
        388542416225042439UL,"/"
       ),
       RepositoryTaglists
      )
     )
    ),
    _Imgur,
    _Discord,
    new DataAccess.TaglistRepositoryMain(@"DataAccess\Taglists.xml"),
    new DataAccess.SettingsRepositoryMain(@"DataAccess\Settings1.xml")
   );
   Core.Run();
  }

  static void JSONDebug(){
   Newtonsoft.Json.JsonConvert.DefaultSettings = ()=>new Newtonsoft.Json.JsonSerializerSettings(){
    ContractResolver=new Imgur.LibraryEnhancements.ImgurErrorJSONContractResolver(),
    //Converters=new List<Newtonsoft.Json.JsonConverter>(){new Newtonsoft.Json.JsonConverter()},
    //SerializationBinder=null,
    Error = (object Origin,Newtonsoft.Json.Serialization.ErrorEventArgs Event)=>{
    }
   };
   var Result=Newtonsoft.Json.JsonConvert.DeserializeObject
   <ModelsImpl.Basic<Imgur.LibraryEnhancements.ImgurError>>
   (
    @"{""data"":{""error"":{""code"":2008,""message"":""You're commenting too fast! Try again in 27 seconds"",""type"":""Exception_CaptioningTooFast"",""exception"":{""wait"":27}},""request"":""\/3\/comment"",""method"":""POST""},""success"":false,""status"":429}"
   );
   Console.WriteLine("Method - {0}",Result.Data.Method);
   Console.WriteLine("Request - {0}",Result.Data.Request);
   Console.WriteLine("Error - {0}",Result.Data.Error);
  }

  static void Main(){
   //AppDomain.CurrentDomain.AssemblyResolve+=ResolveAssembly;
   //new Debug().RunDebug().Wait();
   //new Debug().RunDebugDiscord();
   //new Debug().RunCore();
   //EntryPoint._Main();
   JSONDebug();
   Console.ReadKey(true);
  }

  /*
  private static Assembly ResolveAssembly(object Origin,ResolveEventArgs Event){
   if(Event.Name!="System.Interactive.Async, Version=3.0.1000.0, Culture=neutral, PublicKeyToken=94bc3704cddfc263"){return null;}
   Assembly Result=Assembly.LoadFile(@"\.nuget\packages\system.interactive.async\3.1.1\lib\net46\System.Interactive.Async.dll");
   return Result;
  }
  */
 }

 #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
 class TestImgurInterfacer:Imgur.ImgurInterfacer{
  public bool isCommentByThisApplication(IComment operand){
   return false;
  }
  public async Task<IOAuth2Token> RefreshUserAuthenticationToken() {
   return new ModelsImpl.OAuth2Token("Access Token","Refresh Token","Token Type","Account ID","Account Name",(int)TimeSpan.FromDays(1).TotalSeconds);
  }
  public async Task<IDictionary<string,IList<IComment>>> ReadCommentsSince(DateTimeOffset SinceExclusive,ISet<string> ByUsernames,short RequestsPerUserLimit) {
   return new Dictionary<string,IList<IComment>>(0);
  }
  public async Task<IEnumerable<IComment>> ReadCommentReplies(IComment RepliesTo){
   return new List<IComment>(0);
  }
  public async Task<GalleryItem> ReadGalleryImage(string ID){
   return new GalleryItemDebug(ID,"Title","Image Page URL","Image Resource URL","Author","Description",false,DateTimeOffset.UtcNow,"Categories",800,600);
  }
  public async Task<GalleryItem> ReadGalleryAlbum(string ID){
   return await ReadGalleryImage(ID);
  }
  public async Task MentionUsers(string OnItemID,int ItemParentCommentID,ISet<string> UsernamesToMention) {
   return;
  }
  public async Task<IRateLimit> ReadRemainingBandwidth(){
   return new ModelsImpl.RateLimit(){
    ClientRemaining=999,
    ClientLimit=1000
   };
  }
  public async Task LogRemainingBandwidth(){
   return;
  }
  public async Task LogRemainingBandwidth(TraceEventType Level){
   return;
  }
  public async Task<IAccount> ReadUserDetails(string Username){
   return new ModelsImpl.Account(){
    Url=Username,
    Id=3,
    Created=DateTimeOffset.FromUnixTimeSeconds(0),
    Reputation=0,
    Bio="Bio"
   };
  }
  public async Task<IAccount> ReadUserDetails(int UserID){
   return new ModelsImpl.Account(){
    Url="Username",
    Id=UserID,
    Created=DateTimeOffset.FromUnixTimeSeconds(0),
    Reputation=0,
    Bio="Bio"
   };
  }
  public DateTimeOffset OAuthTokenExpiry{get{
   return DateTimeOffset.UtcNow.AddMonths(1);
  }}
 }
 #pragma warning restore CS1998

 public class GalleryItemDebug:GalleryItem{
  public GalleryItemDebug(string ID,string Title,string LinkPage,string LinkImage,string AuthorUsername,string Description,bool NSFW,DateTimeOffset Created,string Categories,int? Width,int? Height)
  :base(ID,Title,LinkPage,LinkImage,AuthorUsername,Description,NSFW,Created,Categories,Width,Height){}
 }
}
#endif