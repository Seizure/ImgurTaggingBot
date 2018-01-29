using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Tagaroo{
 public class ApplicationConfiguration{
  public SourceLevels LogLevelBootstrap { get; }
  public SourceLevels LogLevelApplication { get; }
  public SourceLevels LogLevelImgur { get; }
  public SourceLevels LogLevelDiscord { get; }
  public SourceLevels LogLevelDiscordLibrary { get; }
  public SourceLevels LogLevelImgurBandwidth { get; }
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
  public ulong DiscordChannelIDCommands { get; }
  public string DiscordCommandPrefix { get; }
  public TimeSpan PullCommentsFrequency{get;}
  public string TaglistDataFilePath { get; }
  public string ImgurCommandPrefix { get; }

  public ApplicationConfiguration(
   SourceLevels logLevelBootstrap,
   SourceLevels logLevelApplication,
   SourceLevels logLevelImgur,
   SourceLevels logLevelDiscord,
   SourceLevels logLevelDiscordLibrary,
   SourceLevels logLevelImgurBandwidth,
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
   ulong DiscordChannelIDCommands,
   string DiscordCommandPrefix,
   TimeSpan PullCommentsFrequency,
   string taglistDataFilePath,
   string ImgurCommandPrefix
   ){
   if(imgurMaximumCommentLengthUTF16CodeUnits<=0){throw new ArgumentOutOfRangeException();}
   this.LogLevelBootstrap=logLevelBootstrap;
   this.LogLevelApplication=logLevelApplication;
   this.LogLevelImgur=logLevelImgur;
   this.LogLevelDiscord=logLevelDiscord;
   this.LogLevelDiscordLibrary=logLevelDiscordLibrary;
   this.LogLevelImgurBandwidth=logLevelImgurBandwidth;
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
   this.DiscordChannelIDCommands=DiscordChannelIDCommands;
   this.DiscordCommandPrefix=DiscordCommandPrefix;
   this.PullCommentsFrequency=PullCommentsFrequency;
   this.TaglistDataFilePath=taglistDataFilePath;
   this.ImgurCommandPrefix=ImgurCommandPrefix;
  }
 }
}