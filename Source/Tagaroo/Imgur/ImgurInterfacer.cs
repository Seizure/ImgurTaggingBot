using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using Imgur.API.Models;
using Imgur.API.Enums;
using Imgur.API.Authentication;
using Imgur.API.Endpoints;
using Tagaroo.Imgur.LibraryEnhancements;
using Tagaroo.Model;
using Tagaroo.DataAccess;
using Tagaroo.Logging;
using Newtonsoft.Json;

using AuthenticationImpl=Imgur.API.Authentication.Impl;
using ModelsImpl=Imgur.API.Models.Impl;
using EndpointsImpl=Imgur.API.Endpoints.Impl;
using ImgurException=Imgur.API.ImgurException;
using HttpRequestException=System.Net.Http.HttpRequestException;

namespace Tagaroo.Imgur{
 
 /// <summary>
 /// The application's interface to the services provided by Imgur.
 /// An <see cref="ImgurException"/> is thrown if there is some problem interacting with the Imgur API;
 /// one may also be thrown in other situations, depending on the method.
 /// </summary>
 public interface ImgurInterfacer{
  /// <summary>
  /// The point in time at which the OAuth Token,
  /// used for accessing the application's associated Imgur account, expires.
  /// Upon relevant method calls, a check will be made to see if the expiry is close,
  /// and if so, an attempt will be made to automatically refresh the OAuth Token.
  /// </summary>
  DateTimeOffset OAuthTokenExpiry{get;}

  /// <summary>
  /// Returns true if the supplied Comment was made by this application;
  /// that is, if its Author matches the Imgur account that the application is associated with.
  /// </summary>
  bool isCommentByThisApplication(IComment operand);
  
  /// <summary>
  /// Refreshes the OAuth Token used to access the application's associated Imgur account.
  /// If successful, the new OAuth Token is saved to the associated <see cref="SettingsRepository"/>.
  /// The new OAuth Token replaces the old one in subsequent method calls.
  /// This method will be called automatically upon relevant method calls
  /// if the expiry date–time is near or passed.
  /// </summary>
  /// <exception cref="ImgurException"/>
  Task<IOAuth2Token> RefreshUserAuthenticationToken();
  
  /// <summary>
  /// Retrieves details of an Imgur user account, by its identifying username.
  /// </summary>
  /// <exception cref="ImgurException">Also thrown if no account with the specified username exists</exception>
  Task<IAccount> ReadUserDetails(string Username);
  /// <summary>
  /// Retrieves details of an Imgur user account, by its identifying numeric account ID.
  /// </summary>
  /// <exception cref="ImgurException">Also thrown if no account with the specified ID exists</exception>
  Task<IAccount> ReadUserDetails(int UserID);

  /// <summary>
  /// Retrieves all Comments, in latest-first order, made by all the Imgur users specified in <paramref name="ByUsernames"/>
  /// since the date–time specified by <paramref name="SinceExclusive"/>.
  /// </summary>
  /// <param name="ByUsernames">The identifying usernames of the Imgur users for which to retrieve Comments for</param>
  /// <param name="SinceExclusive">The furthest point in time to go back to when retrieving Comments for each user; no Comments at or before this point will be returned</param>
  /// <param name="RequestsPerUserLimit">
  /// Safety feature for limiting the Imgur API bandwidth consumed by this call.
  /// If, when retrieving Comments for an individual user, the number of API calls made while retrieving Comments for that user exceeds this limit,
  /// a warning is logged, and no further Comments are retrieved.
  /// If the limit is exceeded, the Comments returned for that user may not be all the Comments they have made since <paramref name="SinceExclusive"/>;
  /// the missing Comments will all be older than the oldest Comment retrieved.
  /// Specifying a non-positive value will disable this feature.
  /// </param>
  /// <returns>A dictionary with one entry for each entry in <paramref name="ByUsernames"/>, the value of each entry being an ordered list of Comments made by that user</returns>
  /// <exception cref="ImgurException"/>
  Task<IDictionary<string,IList<IComment>>> ReadCommentsSince(
   DateTimeOffset SinceExclusive,
   ISet<string> ByUsernames,
   short RequestsPerUserLimit
  );
  
  /// <summary>
  /// Retrieves a collection of Replies made to the supplied Comment.
  /// The supplied Comment is not altered.
  /// </summary>
  /// <exception cref="ImgurException"/>
  Task<IEnumerable<IComment>> ReadCommentReplies(IComment RepliesTo);

  /// <summary>
  /// Retrieves the details of a Gallery Item on Imgur,
  /// specifically an Image with the supplied ID.
  /// </summary>
  /// <exception cref="ImgurException"/>
  Task<GalleryItem> ReadGalleryImage(string ID);
  /// <summary>
  /// Retrieves the details of a Gallery Item on Imgur,
  /// specifically an Album with the supplied ID.
  /// </summary>
  /// <exception cref="ImgurException"/>
  Task<GalleryItem> ReadGalleryAlbum(string ID);
  
  /// <summary>
  /// "Mentions" all the Imgur users in <paramref name="UsernamesToMention"/>
  /// in a Reply to the Comment with ID <paramref name="ItemParentCommentID"/>
  /// which must be on the Gallery Item with ID <paramref name="OnItemID"/>.
  /// Since the length of Comments is limited,
  /// the Mentions to make are split across many Comment Replies if needed.
  /// This is a long-running task;
  /// Imgur enforces a limit on the amount of Comments that can be made in a certain interval,
  /// therefore a delay is needed between Comments made.
  /// </summary>
  /// <exception cref="ImgurException">Also thrown if the Gallery Item specified by <paramref name="OnItemID"/> or the Comment on it specified by <paramref name="ItemParentCommentID"/> could not be found</exception>
  Task MentionUsers(
   string OnItemID,
   int ItemParentCommentID,
   ISet<string> UsernamesToMention
  );

  /// <summary>
  /// Retrieves details of the remaining Imgur API bandwidth available to the application.
  /// </summary>
  /// <exception cref="ImgurException"/>
  Task<IRateLimit> ReadRemainingBandwidth();

  /// <summary>
  /// Logs a message to <see cref="Log.ImgurBandwidth"/> detailing the remaining Imgur API bandwidth available to the application,
  /// which is retrieved via <see cref="ReadRemainingBandwidth"/>,
  /// with the specified logging level.
  /// If the logging level is <see cref="TraceEventType.Information"/>, the message is promoted to <see cref="TraceEventType.Warning"/>
  /// if the remaining bandwidth is below a certain threshhold.
  /// Any errors in acquiring the information are logged.
  /// </summary>
  Task LogRemainingBandwidth(TraceEventType Level);
 }

 public class ImgurInterfacerMain : ImgurInterfacer{
  private readonly IApiClient Client,ClientAuthenticated;
  private readonly IOAuth2Endpoint APIOAuth;
  private readonly IRateLimitEndpoint APIBandwidth;
  private readonly AccountEndpointEnhanced APIUserAccount;
  private readonly ICommentEndpoint APIComments;
  private readonly IImageEndpoint APIImage;
  private readonly IAlbumEndpoint APIAlbum;
  private readonly SettingsRepository RepositorySettings;
  private readonly SemaphoreSlim PostCommentSemaphore = new SemaphoreSlim(1,1);
  private readonly TimeSpan PostCommentDelay;
  private readonly int UserID;
  private readonly float PercentageRemainingAPIBandwidthWarningThreshhold;
  private readonly ushort MaximumCommentLength;
  private readonly string MentionPrefix;
  
  /// <summary>
  /// <para>
  /// Preconditions:
  /// <paramref name="PostCommentDelay"/> is non-negative and not greater than <see cref="Int32.MaxValue"/> milliseconds;
  /// 0 ≤ <paramref name="PercentageRemainingAPIBandwidthWarningThreshhold"/> ≤ 1;
  /// <paramref name="MaximumCommentLength"/> &gt; 0
  /// </para>
  /// </summary>
  /// <param name="ApplicationAuthenticationID">
  /// The "Client ID" portion of the Imgur API Key
  /// with which to connect to the Imgur API with
  /// </param>
  /// <param name="ApplicationAuthenticationSecret">
  /// The "Client Secret" portion of the Imgur API Key
  /// with which to connect to the Imgur API with
  /// </param>
  /// <param name="UserName">
  /// The identifying username of the Imgur account
  /// with which the application is to perform actions as
  /// </param>
  /// <param name="UserID">
  /// The identifying numeric ID of the Imgur account
  /// with which the application is to perform actions as
  /// </param>
  /// <param name="UserAuthenticationToken">
  /// The "Access Token" portion of the OAuth Token
  /// that grants access to the Imgur account to perform actions as
  /// </param>
  /// <param name="UserAuthenticationRefreshToken">
  /// The "Refresh Token" portion of the OAuth Token,
  /// which is used to generate a new "Access Token" when the existing one expires
  /// </param>
  /// <param name="UserAuthenticationTokenType">
  /// The "Token Type" portion of the OAuth Token,
  /// which typically seems to be "bearer"
  /// </param>
  /// <param name="TokenExpiresAt">
  /// The date–time at which the "Access Token" of the OAuth Token expires;
  /// if not known this can simply be set to a date–time in the past
  /// to acquire a new "Access Token" and expiry upon the first call requiring an OAuth Token
  /// </param>
  /// <param name="PostCommentDelay">
  /// The time to wait after posting a Comment before allowing another Comment to be posted
  /// </param>
  /// <param name="PercentageRemainingAPIBandwidthWarningThreshhold">
  /// A percentage value between 0 and 1 inclusive that marks the threshhold
  /// at which <see cref="LogRemainingBandwidth"/> will promote Informational messages to Warnings,
  /// measured as the remaining amount of daily-alloted API bandwidth.
  /// </param>
  /// <param name="MaximumCommentLength">
  /// The maximum permitted length of an Imgur Comment,
  /// which seems to be measured in UTF-16 Code Units
  /// </param>
  /// <param name="MentionPrefix">
  /// The prefix prepended to Imgur usernames in order to mention them;
  /// should normally be "@", but can be changed for testing purposes.
  /// </param>
  /// <param name="RepositorySettings">
  /// A data access repository for the application's settings,
  /// which will be called to save any changes made to the OAuth Token,
  /// which may be updated automatically when close to or passed expiry, or manually
  /// </param>
  public ImgurInterfacerMain(
   SettingsRepository RepositorySettings,
   string ApplicationAuthenticationID,
   string ApplicationAuthenticationSecret,
   string UserName,
   int UserID,
   string UserAuthenticationToken,
   string UserAuthenticationRefreshToken,
   string UserAuthenticationTokenType,
   DateTimeOffset TokenExpiresAt,
   TimeSpan PostCommentDelay,
   float PercentageRemainingAPIBandwidthWarningThreshhold,
   short MaximumCommentLength,
   string MentionPrefix
  ){
   if(PostCommentDelay<TimeSpan.Zero){
    throw new ArgumentOutOfRangeException(nameof(PostCommentDelay));
   }
   if(PercentageRemainingAPIBandwidthWarningThreshhold<0||PercentageRemainingAPIBandwidthWarningThreshhold>1){
    throw new ArgumentOutOfRangeException(nameof(PercentageRemainingAPIBandwidthWarningThreshhold));
   }
   if(MaximumCommentLength<=0){
    throw new ArgumentOutOfRangeException(nameof(MaximumCommentLength));
   }
   DateTimeOffset Now=DateTimeOffset.UtcNow;
   int ExpiryTime;
   try{
    ExpiryTime = checked((int)Math.Floor( (TokenExpiresAt-Now).TotalSeconds ));
   }catch(OverflowException){
    ExpiryTime = TokenExpiresAt>Now ? int.MaxValue : int.MinValue;
   }
   this.Client=new AuthenticationImpl.ImgurClient(
    ApplicationAuthenticationID
   );
   this.ClientAuthenticated=new AuthenticationImpl.ImgurClient(
    ApplicationAuthenticationID,
    ApplicationAuthenticationSecret,
    new ModelsImpl.OAuth2Token(
     UserAuthenticationToken,
     UserAuthenticationRefreshToken,
     UserAuthenticationTokenType,
     UserID.ToString("D",CultureInfo.InvariantCulture),
     UserName,
     ExpiryTime
    )
   );
   this.APIOAuth=new EndpointsImpl.OAuth2Endpoint(ClientAuthenticated);
   this.APIBandwidth=new EndpointsImpl.RateLimitEndpoint(Client);
   this.APIUserAccount=new AccountEndpointEnhanced(Client);
   this.APIComments=new EndpointsImpl.CommentEndpoint(ClientAuthenticated);
   this.APIImage=new EndpointsImpl.ImageEndpoint(Client);
   this.APIAlbum=new EndpointsImpl.AlbumEndpoint(Client);
   this.UserID=UserID;
   this.PostCommentDelay=PostCommentDelay;
   this.PercentageRemainingAPIBandwidthWarningThreshhold=PercentageRemainingAPIBandwidthWarningThreshhold;
   this.MaximumCommentLength=(ushort)MaximumCommentLength;
   this.RepositorySettings=RepositorySettings;
   this.MentionPrefix=MentionPrefix??string.Empty;
   //See ImgurErrorJSONContractResolver for why this is needed
   JsonConvert.DefaultSettings = ()=> new JsonSerializerSettings(){
    ContractResolver = new ImgurErrorJSONContractResolver()
   };
  }

  public DateTimeOffset OAuthTokenExpiry{get{
   return ClientAuthenticated.OAuth2Token.ExpiresAt;
  }}

  public bool isCommentByThisApplication(IComment operand){
   return operand.AuthorId == this.UserID;
  }

  /// <exception cref="ImgurException"/>
  protected Task EnsureUserAuthenticationTokenCurrent(){
   //TODO Log message on imminent OAuth Token expiry
   DateTimeOffset Now=DateTimeOffset.UtcNow;
   if( (ClientAuthenticated.OAuth2Token.ExpiresAt - ExpiryPrecision) <= Now ){
    Log.Imgur_.LogInfo(
     "The current date-time, {1:u}, is close to the expiry date-time of the OAuth Token, {0:u}; attempting a refresh",
     ClientAuthenticated.OAuth2Token.ExpiresAt, Now
    );
    return RefreshUserAuthenticationToken();
   }
   return Task.CompletedTask;
  }

  public async Task<IOAuth2Token> RefreshUserAuthenticationToken(){
   //? API response parameter expires_in seems to be in tenths of a second instead of seconds; possible API bug
   IOAuth2Token NewToken;
   try{
    NewToken = await APIOAuth.GetTokenByRefreshTokenAsync(ClientAuthenticated.OAuth2Token.RefreshToken);
   }catch(ImgurException){
    throw;
   }catch(HttpRequestException Error){
    throw ToImgurException(Error);
   }
   //TODO Security–Convenience issues regarding logging this information to Discord
   Log.Imgur_.LogCritical(
    "A new Imgur OAuth Token has been acquired, which expires at {0:u}. It is highly recommended that this Token be backed up from the settings file, in case it is lost. This token must be kept secret; anyone with access to it has access to the Imgur user account with which it is associated.",
    NewToken.ExpiresAt
    //"A new Imgur OAuth Token has been acquired - '{0}' (Refresh '{1}'), which expires at {2:u}. It is highly recommended that this Token be backed up, in case it is lost. This token must be kept secret; anyone with access to it has access to the Imgur user account with which it is associated.",
    //NewToken.AccessToken,NewToken.RefreshToken,NewToken.ExpiresAt
   );
   ClientAuthenticated.SetOAuth2Token(NewToken);
   try{
    await RepositorySettings.SaveNewImgurUserAuthorizationToken(NewToken);
    Log.Imgur_.LogInfo("New Imgur OAuth Token saved successfully to settings");
   }catch(DataAccessException Error){
    Log.Imgur_.LogCritical(
     "An error occured whilst saving the new Imgur OAuth Token; update the application settings manually with the new Token, otherwise subsequent runs of the application will use the old Token and fail to connect to Imgur.\nDetails: {0}",
     Error.Message
    );
   }
   return NewToken;
  }

  public async Task<IRateLimit> ReadRemainingBandwidth(){
   try{
    return await APIBandwidth.GetRateLimitAsync();
   }catch(ImgurException){
    throw;
   }catch(HttpRequestException Error){
    throw ToImgurException(Error);
   }
  }

  public async Task LogRemainingBandwidth(TraceEventType Level){
   TraceEventType MaximumLevel=Level;
   if(Level==TraceEventType.Information){
    MaximumLevel=TraceEventType.Warning;
   }
   if(!Log.ImgurBandwidth_.ShouldLog(MaximumLevel)){
    return;
   }
   IRateLimit RemainingBandwidth;
   try{
    RemainingBandwidth = await ReadRemainingBandwidth();
   }catch(ImgurException Error){
    Log.ImgurBandwidth_.LogError("Error acquiring API Bandwidth details: "+Error.Message);
    return;
   }
   if(Level==TraceEventType.Information){
    float PercentageRemaining = RemainingBandwidth.ClientRemaining / (float)RemainingBandwidth.ClientLimit;
    if(PercentageRemaining <= PercentageRemainingAPIBandwidthWarningThreshhold) {
     Level = TraceEventType.Warning;
    }
   }
   Log.ImgurBandwidth_.Log(
    Level,
    "Remaining Imgur API Bandwidth - {0:D} / {1:D}",
    RemainingBandwidth.ClientRemaining,RemainingBandwidth.ClientLimit
   );
  }

  public async Task<IAccount> ReadUserDetails(string Username){
   try{
    return await APIUserAccount.GetAccountAsync(Username);
   }catch(ImgurException){
    throw;
   }catch(HttpRequestException Error){
    throw ToImgurException(Error);
   }
  }
  public async Task<IAccount> ReadUserDetails(int UserID){
   try{
    return await APIUserAccount.GetAccountAsync(UserID);
   }catch(ImgurException){
    throw;
   }catch(HttpRequestException Error){
    throw ToImgurException(Error);
   }
  }

  public async Task<IDictionary<string,IList<IComment>>> ReadCommentsSince(
   DateTimeOffset SinceExclusive,
   ISet<string> ByUsernames,
   short RequestsPerUserLimit
  ){
   IDictionary<string,IList<IComment>> Result=new Dictionary<string,IList<IComment>>(ByUsernames.Count);
   foreach(string ByUsername in ByUsernames){
    IList<IComment> UserComments;
    try{
     UserComments = await ReadCommentsSince(SinceExclusive, ByUsername, RequestsPerUserLimit);
    }catch(ImgurException){
     throw;
    }
    Result.Add(ByUsername,UserComments);
   }
   return Result;
  }

  /// <exception cref="ImgurException"/>
  private async Task<IList<IComment>> ReadCommentsSince(
   DateTimeOffset SinceExclusive,
   string ByUsername,
   short RequestsLimit
  ){
   List<IComment> Result=new List<IComment>();
   ushort RequestCount=0;
   int page=0;
   bool more;
   DateTimeOffset OldestCommentDateTime=DateTimeOffset.MinValue;
   do{
    if(RequestCount >= RequestsLimit && RequestsLimit > 0){
     Log.Imgur_.LogWarning(
      "Reached API call limit whilst retrieving Comments for User '{0}' since {1}; not all Comments since that point will have been retrieved and processed",
      ByUsername,SinceExclusive
     );
     break;
    }
    IList<IComment> UserComments;
    try{
     UserComments=(
      //Pages start from 0
      await APIUserAccount.GetCommentsAsync(ByUsername, CommentSortOrder.Newest, page++)
     ).ToList();
    }catch(ImgurException){
     throw;
    }catch(HttpRequestException Error){
     throw ToImgurException(Error);
    }
    Result.AddRange(UserComments);
    more = UserComments.Count >= CommentsPerPage;
    if(UserComments.Count>0){
     OldestCommentDateTime = UserComments.Last().DateTime;
    }
    ++RequestCount;
   //Keep pulling Comments pages until the oldest Comment pulled is from at or before SinceExclusive
   }while(more && OldestCommentDateTime > SinceExclusive);
   //Remove any Comments from the last Comments page that were from at or before SinceExclusive
   return (
    from C in Result
    where C.DateTime > SinceExclusive
    select C
   ).ToList();
  }

  public async Task<IEnumerable<IComment>> ReadCommentReplies(IComment RepliesTo){
   try{
    return (await APIComments.GetRepliesAsync(RepliesTo.Id)).Children;
   }catch(ImgurException){
    throw;
   }catch(HttpRequestException Error){
    throw ToImgurException(Error);
   }
  }

  public Task<GalleryItem> ReadGalleryImage(string ID){
   return ReadGalleryItem(ID,false);
  }
  public Task<GalleryItem> ReadGalleryAlbum(string ID){
   return ReadGalleryItem(ID,true);
  }
  
  /// <exception cref="ImgurException"/>
  protected async Task<GalleryItem> ReadGalleryItem(string ID,bool Album){
   try{
    if(!Album){
     return GalleryItem.FromImgurImage(
      ID,
      await APIImage.GetImageAsync(ID)
     );
    }else{
     IAlbum AlbumModel = await APIAlbum.GetAlbumAsync(ID);
     IImage AlbumCoverImage = (
      from I in AlbumModel.Images
      where string.Equals(I.Id, AlbumModel.Cover, StringComparison.Ordinal)
      select I
     ).FirstOrDefault();
     if(AlbumCoverImage is null){
      Log.Imgur_.LogWarning(
       "The Cover Image with ID '{1}' for the Album with ID '{0}' could not be found",
       AlbumModel.Id,AlbumModel.Cover
      );
     }
     return GalleryItem.FromImgurAlbum(
      ID, AlbumModel, AlbumCoverImage
     );
    }
   }catch(ImgurException){
    throw;
   }catch(HttpRequestException Error){
    throw ToImgurException(Error);
   }
  }

  public async Task MentionUsers(string OnItemID,int ItemParentCommentID,ISet<string> UsernamesToMention){
   try{
    await EnsureUserAuthenticationTokenCurrent();
   }catch(ImgurException){
    throw;
   }
   IList<string> CommentsToMake = ChunkConcatenate(
    UsernamesToMention.ToList(),
    MentionPrefix,
    " ",
    MaximumCommentLength
   );
   Log.Imgur_.LogVerbose("Mentioning {0} total users across {1} total Comment Replies on Gallery Item '{2}'",UsernamesToMention.Count,CommentsToMake.Count,OnItemID);
   int CommentNumber=0;
   foreach(string Comment in CommentsToMake){
    ++CommentNumber;
    try{
     await PostComment(
      Comment,OnItemID,ItemParentCommentID,
      string.Format("Mention Comment {0} of {1} on Gallery Item '{2}' (parent Comment ID {3:D})",CommentNumber,CommentsToMake.Count,OnItemID,ItemParentCommentID)
     );
    }catch(ImgurException){
     throw;
    }
   }
  }

  /// <exception cref="ImgurException"/>
  protected async Task PostComment(string Comment,string OnItemID,int ItemParentCommentID,string LoggedCommentDescription){
   Log.Imgur_.LogVerbose("Waiting to post {0}",LoggedCommentDescription);
   //Allow only a single call to PostComment at a time, in order to properly enforce the delay between posted Comments
   await PostCommentSemaphore.WaitAsync();
   try{
    Log.Imgur_.LogVerbose("Posting {0}",LoggedCommentDescription);
    try{
     await APIComments.CreateCommentAsync(
      Comment, OnItemID, ItemParentCommentID.ToString("D",CultureInfo.InvariantCulture)
     );
    }catch(ImgurException){
     throw;
    }catch(HttpRequestException Error){
     throw ToImgurException(Error);
    }
    Log.Imgur_.LogVerbose("Posted {0}; delaying for {1} before allowing further Comments",LoggedCommentDescription,PostCommentDelay);
    await Task.Delay(PostCommentDelay);
    Log.Imgur_.LogVerbose("Delay complete after {0}; allowing further Comments",LoggedCommentDescription);
   }finally{
    PostCommentSemaphore.Release();
   }
  }

  protected IList<string> ChunkConcatenate(IList<string> Input,string Prepend,string Separator,int MaximumChunkSizeUTF16CodeUnits){
   if(MaximumChunkSizeUTF16CodeUnits<=0){throw new ArgumentOutOfRangeException(nameof(MaximumChunkSizeUTF16CodeUnits));}
   IList<string> Result=new List<string>();
   StringBuilder Current=new StringBuilder();
   bool first = true;
   for(int index=0; index<Input.Count; ++index){
    string InputItem = Input[index];
    string ToAppend = Prepend+InputItem;
    if(!first){
     ToAppend = Separator + ToAppend;
    }
    if(Current.Length + ToAppend.Length > MaximumChunkSizeUTF16CodeUnits){
     if(first){
      Log.Imgur_.LogWarning(
       "The Username '{0}' is too long to fit inside a single Comment, and so will not be Mentioned",
       InputItem
      );
      continue;
     }
     Result.Add(Current.ToString());
     Current.Clear();
     first = true;
     --index;
    }else{
     Current.Append(ToAppend);
     first = false;
    }
   }
   if(Input.Count>0){
    Result.Add(Current.ToString());
   }
   return Result;
  }

  protected ImgurException ToImgurException(HttpRequestException From){
   return new ImgurException("Network Error: "+From.Message,From);
  }

  protected readonly TimeSpan ExpiryPrecision = TimeSpan.FromDays(1);
  protected const int CommentsPerPage = 50;
 }
}