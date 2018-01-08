using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Tagaroo.Application{
 //TODO Non-Empty strings in Settings XML
 public class ApplicationConfiguration{
  public SourceLevels LogLevelBootstrap { get; }
  public SourceLevels LogLevelApplication { get; }
  public SourceLevels LogLevelImgur { get; }
  public SourceLevels LogLevelDiscord { get; }
  public SourceLevels LogLevelDiscordLibrary { get; }
  public bool LogToDiscord { get; }
  public string ImgurClientID { get; }
  public string ImgurClientSecret { get; }
  public string ImgurUserAccountUsername { get; }
  public int ImgurUserAccountID { get; }
  public string ImgurOAuthAccessToken { get; }
  public string ImgurOAuthRefreshToken { get; }
  public string ImgurOAuthTokenType { get; }
  public DateTimeOffset ImgurOAuthTokenExpiry { get; }
  public short ImgurMaximumCommentLengthUTF16CodeUnits { get; }
  public string DiscordAuthenticationToken { get; }
  public ulong DiscordGuildID { get; }
  public ulong DiscordChannelIDLog { get; }
  public string TaglistDataFilePath { get; }

  public ApplicationConfiguration(
   SourceLevels logLevelBootstrap,
   SourceLevels logLevelApplication,
   SourceLevels logLevelImgur,
   SourceLevels logLevelDiscord,
   SourceLevels logLevelDiscordLibrary,
   bool LogToDiscord,
   string imgurClientID,
   string imgurClientSecret,
   string imgurUserAccountUsername,
   int imgurUserAccountID,
   string imgurOAuthAccessToken,
   string imgurOAuthRefreshToken,
   string imgurOAuthTokenType,
   DateTimeOffset imgurOAuthTokenExpiry,
   short imgurMaximumCommentLengthUTF16CodeUnits,
   string discordAuthenticationToken,
   ulong discordGuildID,
   ulong discordChannelIDLog,
   string taglistDataFilePath
   ){
   if(imgurMaximumCommentLengthUTF16CodeUnits<=0){throw new ArgumentOutOfRangeException();}
   this.LogLevelBootstrap=logLevelBootstrap;
   this.LogLevelApplication=logLevelApplication;
   this.LogLevelImgur=logLevelImgur;
   this.LogLevelDiscord=logLevelDiscord;
   this.LogLevelDiscordLibrary=logLevelDiscordLibrary;
   this.LogToDiscord=LogToDiscord;
   this.ImgurClientID=imgurClientID;
   this.ImgurClientSecret=imgurClientSecret;
   this.ImgurUserAccountUsername=imgurUserAccountUsername;
   this.ImgurUserAccountID=imgurUserAccountID;
   this.ImgurOAuthAccessToken=imgurOAuthAccessToken;
   this.ImgurOAuthRefreshToken=imgurOAuthRefreshToken;
   this.ImgurOAuthTokenType=imgurOAuthTokenType;
   this.ImgurOAuthTokenExpiry=imgurOAuthTokenExpiry;
   this.ImgurMaximumCommentLengthUTF16CodeUnits=imgurMaximumCommentLengthUTF16CodeUnits;
   this.DiscordAuthenticationToken=discordAuthenticationToken;
   this.DiscordGuildID=discordGuildID;
   this.DiscordChannelIDLog=discordChannelIDLog;
   this.TaglistDataFilePath=taglistDataFilePath;
  }
 }
}