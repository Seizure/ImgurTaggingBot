using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.IO;
using System.Diagnostics;
using Imgur.API.Models;
using Tagaroo.Application;

namespace Tagaroo.DataAccess{
 
 /// <summary>
 /// Repository/DAO for Model-layer <see cref="Settings"/> and <see cref="ApplicationConfiguration"/> objects.
 /// A <see cref="DataAccessException"/> exception is thrown by many of the methods if there is some problem accessing the underlying data store.
 /// </summary>
 public interface SettingsRepository{
  /// <summary>
  /// Should ideally be called once at some appropriate point after the Repository has been constructed,
  /// allowing realizations to perform nontrivial initialization.
  /// However, realizations should not rely on this method being called before any other methods.
  /// </summary>
  void Initialize();
  
  /// <summary>
  /// Read method for <see cref="ApplicationConfiguration"/>.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  Task<ApplicationConfiguration> LoadConfiguration();
  
  /// <summary>
  /// Read method for <see cref="Settings"/>.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  Task<Settings> LoadSettings();
  
  /// <summary>
  /// Update method for <see cref="Settings"/>,
  /// persisting changes made to most of the object's properties;
  /// see individual <see cref="Settings"/> properties for details.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  Task SaveWritableSettings(Settings ToSave);

  /// <summary>
  /// Special update method for persisting details of a new Imgur OAuth Token.
  /// The new OAuth Token will be visible in the ImgurOAuth* properties of <see cref="ApplicationConfiguration"/>,
  /// returned by subsequent calls made to <see cref="LoadConfiguration"/>.
  /// Imgur OAuth Tokens expire after a certain period of time,
  /// after which a new one must be generated via a process known as "refreshing".
  /// This method allows the application to automatically "refresh" the Imgur OAuth Token
  /// and store its new value without user intervention.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  Task SaveNewImgurUserAuthorizationToken(IOAuth2Token AuthorizationToken);
 }
 
 /// <summary>
 /// Main Realization of <see cref="SettingsRepository"/>,
 /// implementing a Repository that uses an XML file as the persistent data store.
 /// XML instance documents must be valid with respect to the XML Schema in the file Settings.xsd.
 /// The <see cref="SaveWritableSettings"/> method overwrites the "DynamicSettings" element.
 /// The <see cref="SaveNewImgurUserAuthorizationToken"/> method overwrites the "OAuthToken" element.
 /// </summary>
 public class SettingsRepositoryMain : SettingsRepository{
  private readonly XMLFileRepository XMLDataFileHandler;
  private readonly string SettingsFilePath;
  
  /// <param name="SettingsFilePath">The file path to the XML instance document serving as the data store</param>
  public SettingsRepositoryMain(string SettingsFilePath){
   this.XMLDataFileHandler=new XMLFileRepository(SettingsFilePath,xmlns,"Tagaroo.DataAccess.Settings.xsd");
   this.SettingsFilePath=SettingsFilePath;
  }

  public void Initialize(){
   XMLDataFileHandler.Initialize();
  }

  public async Task<ApplicationConfiguration> LoadConfiguration(){
   XDocument DataDocument=await XMLDataFileHandler.LoadFile(FileAccess.Read,FileShare.Read);
   XElement ConfigurationElement=DataDocument.Root.Elements(xmlns+"Configuration").First();
   XElement ConfigurationImgurElement=ConfigurationElement.Elements(xmlns+"Imgur").First();
   XElement ConfigurationImgurOAuthElement=ConfigurationImgurElement.Elements(xmlns+"OAuthToken").First();
   XElement ConfigurationDiscordElement=ConfigurationElement.Elements(xmlns+"Discord").First();
   XElement ConfigurationLoggingElement=ConfigurationElement.Elements(xmlns+"Logging").First();
   XElement StaticSettingsElement=DataDocument.Root.Elements(xmlns+"StaticSettings").First();
   return new ApplicationConfiguration(
    Enum.Parse<SourceLevels>((string)ConfigurationLoggingElement.Attribute("BootstrapLogLevel")),
    Enum.Parse<SourceLevels>((string)ConfigurationLoggingElement.Attribute("ApplicationLogLevel")),
    Enum.Parse<SourceLevels>((string)ConfigurationLoggingElement.Attribute("ImgurLogLevel")),
    Enum.Parse<SourceLevels>((string)ConfigurationLoggingElement.Attribute("DiscordLogLevel")),
    Enum.Parse<SourceLevels>((string)ConfigurationLoggingElement.Attribute("DiscordLibraryLogLevel")),
    Enum.Parse<SourceLevels>((string)ConfigurationLoggingElement.Attribute("ImgurBandwidthLogLevel")),
    (bool)ConfigurationLoggingElement.Attribute("LogToDiscord"),
    (string)ConfigurationImgurElement.Attribute("ClientID"),
    (string)ConfigurationImgurElement.Attribute("ClientSecret"),
    (string)ConfigurationImgurOAuthElement.Attribute("Username"),
    (int)ConfigurationImgurOAuthElement.Attribute("UserID"),
    (string)ConfigurationImgurOAuthElement.Attribute("AccessToken"),
    (string)ConfigurationImgurOAuthElement.Attribute("RefreshToken"),
    (string)ConfigurationImgurOAuthElement.Attribute("TokenType"),
    (DateTimeOffset)ConfigurationImgurOAuthElement.Attribute("ExpiresAt"),
    //Will always be positive and within range due to the schema
    (TimeSpan)StaticSettingsElement.Attribute("ImgurCommentPostingDelay"),
    //Will always be within the inclusive range 0–100 due to the schema
    (short)StaticSettingsElement.Attribute("ImgurAPIBandwidthWarningThreshholdPercentage")/100F,
    //Will always be positive and no greater than 2^31-1 milliseconds due to the schema
    (short)StaticSettingsElement.Attribute("ImgurMaximumCommentLengthUTF16CodeUnits"),
    (string)ConfigurationDiscordElement.Attribute("Token"),
    (ulong)ConfigurationDiscordElement.Attribute("GuildID"),
    (ulong)ConfigurationDiscordElement.Attribute("LogChannelID"),
    (ulong)ConfigurationDiscordElement.Attribute("CommandChannelID"),
    (string)StaticSettingsElement.Attribute("DiscordCommandPrefix"),
    (string)StaticSettingsElement.Attribute("TaglistDatafilePath"),
    (string)StaticSettingsElement.Attribute("ImgurCommandPrefix"),
    (string)StaticSettingsElement.Attribute("ImgurMentionPrefix")
   );
  }

  public async Task<Settings> LoadSettings(){
   Settings Result=new Settings();
   XDocument DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read);
   XElement DynamicSettingsElement = DataDocument.Root.Elements(xmlns+"DynamicSettings").First();
   XElement StaticSettingsElement = DataDocument.Root.Elements(xmlns+"StaticSettings").First();
   return new Settings(){
    //Will always be positive and within range due to the schema
    PullCommentsFrequency = (TimeSpan)StaticSettingsElement.Attribute("PullCommentsFrequency"),
    CommentsProcessedUpToInclusive = (DateTimeOffset)DynamicSettingsElement.Attribute("CommentsProcessedUpToInclusive"),
    RequestThreshholdPullCommentsPerUser = (byte)(short)DynamicSettingsElement.Attribute("PullCommentsPerUserRequestThreshhold"),
    CommenterUsernames = (
     from C in DynamicSettingsElement.Elements(xmlns+"ImgurCommenters").Elements(xmlns+"Commenter")
     select (string)C.Attribute("Username")
    ).ToHashSet()
   };
  }

  public async Task SaveWritableSettings(Settings ToSave){
   XDocument WholeDocument = await XMLDataFileHandler.LoadFile(FileAccess.ReadWrite,FileShare.Read);
   XElement DynamicSettingsElement = WholeDocument.Root.Elements(xmlns+"DynamicSettings").First();
   DynamicSettingsElement.Element(xmlns+"ImgurCommenters").ReplaceNodes(
    from C in ToSave.CommenterUsernames
    select new XElement(xmlns+"Commenter",new XAttribute("Username",C))
   );
   //DynamicSettingsElement.SetAttributeValue("PullCommentsFrequency", ToSave.PullCommentsFrequency);
   DynamicSettingsElement.SetAttributeValue("CommentsProcessedUpToInclusive", ToSave.CommentsProcessedUpToInclusive);
   DynamicSettingsElement.SetAttributeValue("PullCommentsPerUserRequestThreshhold", ToSave.RequestThreshholdPullCommentsPerUser);
   await XMLDataFileHandler.Save(WholeDocument, SavingOptions);
  }

  public async Task SaveNewImgurUserAuthorizationToken(IOAuth2Token AuthorizationToken){
   XDocument WholeDocument = await XMLDataFileHandler.LoadFile(FileAccess.ReadWrite,FileShare.Read);
   XElement OAuthElement = WholeDocument.Root.Elements(xmlns+"Configuration").Elements(xmlns+"Imgur").Elements(xmlns+"OAuthToken").First();
   OAuthElement.SetAttributeValue("AccessToken", AuthorizationToken.AccessToken);
   OAuthElement.SetAttributeValue("RefreshToken", AuthorizationToken.RefreshToken);
   OAuthElement.SetAttributeValue("TokenType", AuthorizationToken.TokenType);
   OAuthElement.SetAttributeValue("ExpiresAt", AuthorizationToken.ExpiresAt);
   await XMLDataFileHandler.Save(WholeDocument, SavingOptions);
  }

  protected const SaveOptions SavingOptions=SaveOptions.DisableFormatting|SaveOptions.OmitDuplicateNamespaces;
  /// <summary>
  /// The expected XML namespace of the root element of instance documents, as defined in Settings.xsd.
  /// </summary>
  static protected readonly XNamespace xmlns="urn:xmlns:tagaroo:Settings:v1.0";
 }
}