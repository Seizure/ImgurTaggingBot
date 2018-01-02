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
using Imgur.API.Models;

using Settings=Tagaroo.Application.Settings;

namespace Tagaroo.DataAccess{
 
 public interface SettingsRepository{
  void Initialize();
  
  /// <exception cref="DataAccessException"/>
  Task LoadConfiguration();
  
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

  //TODO Call on startup
  public void Initialize(){
   XMLDataFileHandler.Initialize();
  }

  public async Task LoadConfiguration(){
   XDocument DataDocument=await XMLDataFileHandler.LoadFile(FileAccess.Read,FileShare.Read);
   XElement ConfigurationImgurElement=DataDocument.Root.Elements(xmlns+"Configuration").Elements(xmlns+"Imgur").First();
   XElement ConfigurationDiscordElement=DataDocument.Root.Elements(xmlns+"Configuration").Elements(xmlns+"Discord").First();
   XElement StaticSettingsElement=DataDocument.Root.Elements(xmlns+"StaticSettings").First();

  }

  public async Task<Settings> LoadSettings(){
   Settings Result=new Settings();
   XDocument DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read);
   XElement DynamicSettingsElement = DataDocument.Root.Elements(xmlns+"DynamicSettings").First();
   return new Settings(){
    PullCommentsFrequency = (TimeSpan)DynamicSettingsElement.Attribute("PullCommentsFrequency"),
    CommentsProcessedUpToInclusive = (DateTimeOffset)DynamicSettingsElement.Attribute("CommentsProcessedUpToInclusive"),
    RequestThreshholdPullCommentsPerUser = (byte)(short)DynamicSettingsElement.Attribute("PullCommentsPerUserRequestThreshhold"),
    CommenterUsernames = (
     from C in DynamicSettingsElement.Elements(xmlns+"ImgurCommenters").Elements(xmlns+"Commenter")
     select (string)C.Attribute("Username")
    ).ToHashSet()
   };
  }

  public async Task SaveWritableSettings(Settings ToSave){
   Stream DataFile = XMLDataFileHandler.OpenFile(FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
   try{
    XDocument WholeDocument = await XMLDataFileHandler.Load(DataFile);
    XElement DynamicSettingsElement = WholeDocument.Root.Elements(xmlns+"DynamicSettings").First();
    DynamicSettingsElement.Element(xmlns+"ImgurCommenters").ReplaceNodes(
     from C in ToSave.CommenterUsernames
     select new XElement(xmlns+"Commenter",new XAttribute("Username",C))
    );
    DynamicSettingsElement.SetAttributeValue("PullCommentsFrequency", ToSave.PullCommentsFrequency);
    DynamicSettingsElement.SetAttributeValue("CommentsProcessedUpToInclusive", ToSave.CommentsProcessedUpToInclusive);
    DynamicSettingsElement.SetAttributeValue("PullCommentsPerUserRequestThreshhold", ToSave.RequestThreshholdPullCommentsPerUser);
    //TODO Backup old data
    DataFile.SetLength(0);
    await WholeDocument.SaveAsync(DataFile,SavingOptions,CancellationToken.None);
   }catch(IOException Error){
    throw new DataAccessException("IO error whilst loading/saving data file: "+Error.Message,Error);
   }finally{
    DataFile.Close();
   }
  }

  public async Task SaveNewImgurUserAuthorizationToken(IOAuth2Token AuthorizationToken){
   Stream DataFile = XMLDataFileHandler.OpenFile(FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
   try{
    XDocument WholeDocument = await XMLDataFileHandler.Load(DataFile);
    XElement OAuthElement = WholeDocument.Root.Elements(xmlns+"Configuration").Elements(xmlns+"Imgur").Elements(xmlns+"OAuthToken").First();
    OAuthElement.SetAttributeValue("AccessToken", AuthorizationToken.AccessToken);
    OAuthElement.SetAttributeValue("RefreshToken", AuthorizationToken.RefreshToken);
    OAuthElement.SetAttributeValue("TokenType", AuthorizationToken.TokenType);
    OAuthElement.SetAttributeValue("ExpiresAt", AuthorizationToken.ExpiresAt);
    //TODO Backup old data
    DataFile.SetLength(0);
    await WholeDocument.SaveAsync(DataFile,SavingOptions,CancellationToken.None);
   }catch(IOException Error){
    throw new DataAccessException("IO error whilst loading/saving data file: "+Error.Message,Error);
   }finally{
    DataFile.Close();
   }
  }

  private const SaveOptions SavingOptions=SaveOptions.DisableFormatting|SaveOptions.OmitDuplicateNamespaces;
  static protected readonly XNamespace xmlns="urn:xmlns:tagaroo:Settings";
 }
}