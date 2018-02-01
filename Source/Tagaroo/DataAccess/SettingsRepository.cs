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
 
 public interface SettingsRepository{
  void Initialize();
  
  /// <exception cref="DataAccessException"/>
  Task<ApplicationConfiguration> LoadConfiguration();
  
  /// <exception cref="DataAccessException"/>
  Task<Settings> LoadSettings();
  
  /// <exception cref="DataAccessException"/>
  Task SaveWritableSettings(Settings ToSave);

  /// <exception cref="DataAccessException"/>
  Task SaveNewImgurUserAuthorizationToken(IOAuth2Token AuthorizationToken);
 }
 
 public class SettingsRepositoryMain : SettingsRepository{
  private readonly XMLFileRepository XMLDataFileHandler;
  private readonly string SettingsFilePath;
  
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
    (short)StaticSettingsElement.Attribute("ImgurMaximumCommentLengthUTF16CodeUnits"),
    (string)ConfigurationDiscordElement.Attribute("Token"),
    (ulong)ConfigurationDiscordElement.Attribute("GuildID"),
    (ulong)ConfigurationDiscordElement.Attribute("LogChannelID"),
    (ulong)ConfigurationDiscordElement.Attribute("CommandChannelID"),
    (string)StaticSettingsElement.Attribute("DiscordCommandPrefix"),
    (TimeSpan)StaticSettingsElement.Attribute("PullCommentsFrequency"),
    (string)StaticSettingsElement.Attribute("TaglistDatafilePath"),
    (string)StaticSettingsElement.Attribute("ImgurCommandPrefix")
   );
  }

  public async Task<Settings> LoadSettings(){
   Settings Result=new Settings();
   XDocument DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read);
   XElement DynamicSettingsElement = DataDocument.Root.Elements(xmlns+"DynamicSettings").First();
   return new Settings(){
    //PullCommentsFrequency = (TimeSpan)DynamicSettingsElement.Attribute("PullCommentsFrequency"),
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
  static protected readonly XNamespace xmlns="urn:xmlns:tagaroo:Settings:v1.0";
 }
}