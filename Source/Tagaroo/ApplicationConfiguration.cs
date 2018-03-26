using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Tagaroo{
 /// <summary>
 /// Model class for the application's Configuration,
 /// key behavioral and important settings determining how the application is set up and run,
 /// including API keys and logging levels.
 /// Instances of this class are typically retrieved from the <see cref="DataAccess.SettingsRepository"/>.
 /// </summary>
 public class ApplicationConfiguration{
  /// <summary>
  /// The verbosity level at which to log messages sent to the Bootstrap logger.
  /// </summary>
  public SourceLevels LogLevelBootstrap { get; }
  /// <summary>
  /// The verbosity level at which to log messages sent to the Application logger.
  /// </summary>
  public SourceLevels LogLevelApplication { get; }
  /// <summary>
  /// The verbosity level at which to log messages sent to the Imgur logger.
  /// </summary>
  public SourceLevels LogLevelImgur { get; }
  /// <summary>
  /// The verbosity level at which to log messages sent to the Discord logger.
  /// </summary>
  public SourceLevels LogLevelDiscord { get; }
  /// <summary>
  /// The verbosity level at which to log messages sent to the Discord Library logger;
  /// since this logger is logged to by a third-party library, with different interpretations of the various verbosity levels,
  /// this should typically be set to a low level of verbosity.
  /// </summary>
  public SourceLevels LogLevelDiscordLibrary { get; }
  /// <summary>
  /// The verbosity level at which to log messages sent to the Imgur API Bandwidth logger.
  /// </summary>
  public SourceLevels LogLevelImgurBandwidth { get; }
  /// <summary>
  /// If the application should send all logging output to the Discord Guild with which it is associated when connected to Discord,
  /// otherwise all logging output will be sent to the process' STDOUT.
  /// When not connected to Discord, logging output falls back to the process' STDOUT.
  /// </summary>
  public bool LogToDiscord { get; }
  /// <summary>
  /// The "Client ID" part of the Imgur API key.
  /// </summary>
  public string ImgurClientID { get; }
  /// <summary>
  /// The "Client Secret" part of the Imgur API key.
  /// </summary>
  public string ImgurClientSecret { get; }
  /// <summary>
  /// The username of the Imgur account for which the Imgur OAuth Token grants access to.
  /// </summary>
  public string ImgurUserAccountUsername { get; }
  /// <summary>
  /// The numeric ID of the Imgur account for which the Imgur OAuth Token grants access to.
  /// </summary>
  public int ImgurUserAccountID { get; }
  /// <summary>
  /// The "Access Token" part of the Imgur OAuth Token.
  /// </summary>
  public string ImgurOAuthAccessToken { get; }
  /// <summary>
  /// The "Refresh Token" part of the Imgur OAuth Token.
  /// </summary>
  public string ImgurOAuthRefreshToken { get; }
  /// <summary>
  /// The "Type" part of the Imgur OAuth Token.
  /// </summary>
  public string ImgurOAuthTokenType { get; }
  /// <summary>
  /// The point at which the Imgur OAuth Token expires,
  /// at which point the "Access Token" ceases to be valid,
  /// and a new "Access Token" and "Refresh Token" must be acquired using the existing "Refresh Token".
  /// </summary>
  public DateTimeOffset ImgurOAuthTokenExpiry { get; }
  /// <summary>
  /// A percentage value between 0 and 1 inclusive.
  /// The percentage level of remaining Imgur API bandwidth
  /// at which Informational level log messages regarding remaining bandwidth
  /// should be promoted to Warnings.
  /// </summary>
  public float ImgurAPIBandwidthWarningThreshhold { get; }
  /// <summary>
  /// Always positive.
  /// The maximum permissible length of a single Imgur Comment,
  /// measured in UTF-16 Code Units.
  /// </summary>
  public short ImgurMaximumCommentLengthUTF16CodeUnits { get; }
  /// <summary>
  /// The string to prefix to Imgur usernames in order to Mention them in Comments and such.
  /// Should be "@", but a different value can be specified for testing purposes,
  /// so that real users don't get Mentioned during any tests.
  /// </summary>
  public string ImgurMentionPrefix{ get; }
  /// <summary>
  /// The "Token" part of the Discord API key
  /// (different from the Client ID and Client Secret).
  /// </summary>
  public string DiscordAuthenticationToken { get; }
  /// <summary>
  /// The unique ID of the Discord Guild with which the application is associated.
  /// </summary>
  public ulong DiscordGuildID { get; }
  /// <summary>
  /// The unique ID of the Text Channel in the Discord Guild
  /// where logging output will be written, if <see cref="LogToDiscord"/> is true.
  /// </summary>
  public ulong DiscordChannelIDLog { get; }
  /// <summary>
  /// The unique ID of the Text Channel in the Discord Guild
  /// from which Discord Commands will be read and executed by the application.
  /// </summary>
  public ulong DiscordChannelIDCommands { get; }
  /// <summary>
  /// A string prefix which must appear at the beginning of all Discord Commands
  /// issued to the application from the <see cref="DiscordChannelIDCommands"/> Channel in Discord.
  /// </summary>
  public string DiscordCommandPrefix { get; }
  /// <summary>
  /// A string prefix which must appear at the beginning of all Imgur Commands
  /// issued to the application from Imgur Comments made by authorized Commenters.
  /// </summary>
  public string ImgurCommandPrefix { get; }
  /// <summary>
  /// The file name and path to the data file containing all the Taglists that the application will manage,
  /// an XML instance document of the XML Schema in Taglists.xsd.
  /// </summary>
  public string TaglistDataFilePath { get; }

  /// <summary>
  /// <para>
  /// Preconditions: <paramref name="imgurMaximumCommentLengthUTF16CodeUnits"/> is positive;
  /// 0 ≤ <paramref name="ImgurAPIBandwidthWarningThreshhold"/> ≤ 1
  /// </para>
  /// </summary>
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
   float ImgurAPIBandwidthWarningThreshhold,
   short imgurMaximumCommentLengthUTF16CodeUnits,
   string discordAuthenticationToken,
   ulong discordGuildID,
   ulong discordChannelIDLog,
   ulong DiscordChannelIDCommands,
   string DiscordCommandPrefix,
   string taglistDataFilePath,
   string ImgurCommandPrefix,
   string ImgurMentionPrefix
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
   this.ImgurAPIBandwidthWarningThreshhold=ImgurAPIBandwidthWarningThreshhold;
   this.ImgurMaximumCommentLengthUTF16CodeUnits=imgurMaximumCommentLengthUTF16CodeUnits;
   this.DiscordAuthenticationToken=discordAuthenticationToken;
   this.DiscordGuildID=discordGuildID;
   this.DiscordChannelIDLog=discordChannelIDLog;
   this.DiscordChannelIDCommands=DiscordChannelIDCommands;
   this.DiscordCommandPrefix=DiscordCommandPrefix;
   this.TaglistDataFilePath=taglistDataFilePath;
   this.ImgurCommandPrefix=ImgurCommandPrefix;
   this.ImgurMentionPrefix=ImgurMentionPrefix;
  }
 }
}