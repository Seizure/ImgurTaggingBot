using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;
using Imgur.API.Models;
using Imgur.API.Enums;
using Imgur.API.Authentication;
using Imgur.API.Endpoints;
using Tagaroo.Model;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

using AuthenticationImpl=Imgur.API.Authentication.Impl;
using ModelsImpl=Imgur.API.Models.Impl;
using EndpointsImpl=Imgur.API.Endpoints.Impl;
using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Imgur{
 
 public interface ImgurInterfacer{
  bool isCommentByThisApplication(IComment operand);
  
  /// <exception cref="ImgurException"/>
  Task<IOAuth2Token> RefreshUserAuthenticationToken();
  
  /// <exception cref="ImgurException"/>
  Task<IDictionary<string,IList<IComment>>> ReadCommentsSince(
   DateTimeOffset SinceExclusive,
   ISet<string> ByUsernames,
   short RequestsPerUserLimit
  );
  
  /// <exception cref="ImgurException"/>
  Task<GalleryItem> ReadGalleryImage(string ID);
  
  /// <exception cref="ImgurException"/>
  Task<GalleryItem> ReadGalleryAlbum(string ID);
  
  /// <exception cref="ImgurException"/>
  Task MentionUsers(
   string OnItemID,
   int ItemParentCommentID,
   ISet<string> UsernamesToMention
  );
 }

 //TODO User ID lookup
 //TODO Query Rate remaining
 public class ImgurInterfacerMain:ImgurInterfacer{
  private readonly IApiClient Client,ClientAuthenticated;
  private readonly IOAuth2Endpoint APIOAuth;
  private readonly IAccountEndpoint APIUserAccount;
  private readonly ICommentEndpoint APIComments;
  private readonly IImageEndpoint APIImage;
  private readonly IAlbumEndpoint APIAlbum;
  private readonly SettingsRepository RepositorySettings;
  private readonly int UserID;
  private readonly int MaximumCommentLength;
  
  /// <summary></summary>
  /// <param name="ApplicationAuthenticationID">
  /// The "Client ID" portion of the Imgur API Key
  /// with which to connect to Imgur's API with
  /// </param>
  /// <param name="ApplicationAuthenticationSecret">
  /// The "Client Secret" portion of the Imgur API Key
  /// with which to connect to Imgur's API with
  /// </param>
  /// <param name="UserName">
  /// The Username of the Imgur account
  /// with which the application is to perform actions as
  /// </param>
  /// <param name="UserID">
  /// The User ID of the Imgur account
  /// with which the application is to perform actions as
  /// </param>
  /// <param name="UserAuthenticationToken">
  /// The "Access Token" portion of the OAuth Token
  /// that grants access to the Imgur user account to perform actions as
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
  /// to acquire a new "Access Token" and expiry upon the first connect
  /// </param>
  /// <param name="MaximumCommentLength">
  /// The maximum permitted length of an Imgur Comment,
  /// which seems to be measured in UTF-16 Code Units
  /// </param>
  /// <param name="RepositorySettings">
  /// A data access repository for the application's settings,
  /// which will be called to save any changes made to the OAuth key,
  /// which may be updated automatically when expired, or manually
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
   int MaximumCommentLength
  ){
   if(MaximumCommentLength<=0){
    throw new ArgumentOutOfRangeException(nameof(MaximumCommentLength));
   }
   DateTimeOffset Now=DateTimeOffset.UtcNow;
   int ExpiryTime;
   try{
    ExpiryTime=(int)Math.Floor( (TokenExpiresAt-Now).TotalSeconds );
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
   this.APIUserAccount=new EndpointsImpl.AccountEndpoint(Client);
   this.APIComments=new EndpointsImpl.CommentEndpoint(ClientAuthenticated);
   this.APIImage=new EndpointsImpl.ImageEndpoint(Client);
   this.APIAlbum=new EndpointsImpl.AlbumEndpoint(Client);
   this.UserID=UserID;
   this.MaximumCommentLength=MaximumCommentLength;
   this.RepositorySettings=RepositorySettings;
  }

  public bool isCommentByThisApplication(IComment operand){
   return operand.AuthorId == this.UserID;
  }

  /// <exception cref="ImgurException"/>
  protected async Task EnsureUserAuthenticationTokenCurrent(){
   if( (ClientAuthenticated.OAuth2Token.ExpiresAt - ExpiryPrecision) <= DateTimeOffset.UtcNow ){
    await RefreshUserAuthenticationToken();
   }
  }

  public async Task<IOAuth2Token> RefreshUserAuthenticationToken(){
   IOAuth2Token NewToken;
   try{
    NewToken = await APIOAuth.GetTokenByRefreshTokenAsync(ClientAuthenticated.OAuth2Token.RefreshToken);
   }catch(ImgurException){
    throw;
   }
   //TODO Security–Convenience issues regarding logging this information to Discord
   Log.Imgur_.LogCritical(
    "A new Imgur OAuth Token has been acquired - '{0}' (Refresh '{1}'), which expires at {2:u}. It is highly recommended that this Token be backed up, in case it is lost. This token must be kept secret; anyone with access to it has access to the Imgur user account with which it is associated.",
    NewToken.AccessToken,NewToken.RefreshToken,NewToken.ExpiresAt
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
    }
    Result.AddRange(UserComments);
    if(UserComments.Count>0){
     OldestCommentDateTime = UserComments.Last().DateTime;
    }else{
     //No more Comments to retrieve
     break;
    }
    ++RequestCount;
   //Keep pulling Comments pages until the oldest Comment pulled is from at or before SinceExclusive
   }while(OldestCommentDateTime > SinceExclusive);
   //Remove any Comments from the last Comments page that were from at or before SinceExclusive
   return (
    from C in Result
    where C.DateTime > SinceExclusive
    select C
   ).ToList();
  }

  public async Task<GalleryItem> ReadGalleryImage(string ID){
   return await ReadGalleryItem(ID,false);
  }
  public async Task<GalleryItem> ReadGalleryAlbum(string ID){
   return await ReadGalleryItem(ID,true);
  }
  
  /// <exception cref="ImgurException"/>
  protected async Task<GalleryItem> ReadGalleryItem(string ID,bool Album){
   try{
    if(!Album){
     return GalleryItem.FromImgurImage(
      await APIImage.GetImageAsync(ID)
     );
    }else{
     return GalleryItem.FromImgurAlbum(
      await APIAlbum.GetAlbumAsync(ID)
     );
    }
   }catch(ImgurException){
    throw;
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
   foreach(string Comment in CommentsToMake){
    try{
     await APIComments.CreateCommentAsync(
      Comment, OnItemID, ItemParentCommentID.ToString("D",CultureInfo.InvariantCulture)
     );
    }catch(ImgurException){
     throw;
    }
   }
  }

  protected IList<string> ChunkConcatenate(IList<string> Input,string Prepend,string Separator,int MaximumChunkSizeUTF16CodeUnits){
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

  #if RELEASE
  //protected const string MentionPrefix = "@";
  #else
  protected const string MentionPrefix = "$";
  #endif
  protected readonly TimeSpan ExpiryPrecision = TimeSpan.FromDays(1);
 }
}