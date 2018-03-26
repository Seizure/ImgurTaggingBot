using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using Tagaroo.Application;
using Tagaroo.DataAccess;
using Tagaroo.Imgur;
using Tagaroo.Discord;
using Tagaroo.Logging;

namespace Tagaroo{
 /// <summary>
 /// The "Main" class of the application.
 /// Responsible for constructing an instance of <see cref="Program"/>
 /// along with all of its dependencies, and running it.
 /// Reads and applies <see cref="ApplicationConfiguration"/>, from the Configuration element of Settings.xml.
 /// </summary>
 static internal class EntryPoint{

  /// <returns>
  /// Non-zero <see cref="Return_ConfigurationLoadError"/> if there is a problem reading Configuration data from Settings.xml;
  /// non-zero <see cref="Return_ApplicationStartError"/> if there is some other problem during startup,
  /// 0 otherwise
  /// </returns>
  public static int _Main(string SettingsFilePath=null){
   //Log to STDOUT by default
   Log.Instance.AddTraceListener(new TextWriterTraceListener(Console.Out,"StdOutListener"));
   //Log all messages sent to the Bootstrap logger until the application's Configuration can be read and applied (other loggers are not used until after the Configuration is applied)
   Log.Instance.BootstrapLevel.Level = SourceLevels.Verbose;
   Log.Bootstrap_.LogInfo("Application starting");
   //Robustness — Use locale-invariant comparison/formatting/e.t.c. rules by default, to mitigate aginst bugs caused by varying host system locales
   CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
   SettingsRepository RepositorySettings=new SettingsRepositoryMain(
    SettingsFilePath ?? DefaultSettingsFilePath
   );
   Log.Bootstrap_.LogVerbose("Initializing Settings repository");
   RepositorySettings.Initialize();
   Log.Bootstrap_.LogInfo("Loading Configuration");
   ApplicationConfiguration Configuration;
   try{
    Configuration = RepositorySettings.LoadConfiguration().GetAwaiter().GetResult();
   }catch(DataAccessException Error){
    Log.Bootstrap_.LogError("Unable to start; could not load application configuration: "+Error.Message);
    return Return_ConfigurationLoadError;
   }
   Log.Bootstrap_.LogInfo("Applying Configuration");
   Log.Bootstrap_.LogVerbose("Applying Configuration: Logging");
   Log.Instance.BootstrapLevel.Level      = Configuration.LogLevelBootstrap;
   Log.Instance.ApplicationLevel.Level    = Configuration.LogLevelApplication;
   Log.Instance.ImgurLevel.Level          = Configuration.LogLevelImgur;
   Log.Instance.DiscordLevel.Level        = Configuration.LogLevelDiscord;
   Log.Instance.DiscordLibraryLevel.Level = Configuration.LogLevelDiscordLibrary;
   Log.Instance.ImgurBandwidthLevel.Level = Configuration.LogLevelImgurBandwidth;
   Log.Bootstrap_.LogVerbose("Applying Configuration: Constructing Application");
   TaglistRepository RepositoryTaglists=new TaglistRepositoryMain(
    Configuration.TaglistDataFilePath
   );
   CacheingTaglistRepository RepositoryTaglistsCacheing=new CacheingTaglistRepository(RepositoryTaglists);
   ImgurInterfacer Imgur=new ImgurInterfacerMain(
    RepositorySettings,
    Configuration.ImgurClientID,
    Configuration.ImgurClientSecret,
    Configuration.ImgurUserAccountUsername,
    Configuration.ImgurUserAccountID,
    Configuration.ImgurOAuthAccessToken,
    Configuration.ImgurOAuthRefreshToken,
    Configuration.ImgurOAuthTokenType,
    Configuration.ImgurOAuthTokenExpiry,
    Configuration.ImgurMaximumCommentLengthUTF16CodeUnits,
    Configuration.ImgurMentionPrefix
   );
   DiscordInterfacer Discord=new DiscordInterfacerMain(
    Configuration.DiscordAuthenticationToken,
    Configuration.DiscordGuildID,
    Configuration.DiscordChannelIDLog,
    Configuration.DiscordChannelIDCommands,
    Configuration.DiscordCommandPrefix
   );
   Program Application=new Program(
    new ProcessLatestCommentsActivity(
     Imgur, RepositorySettings, RepositoryTaglistsCacheing,
     new ProcessCommentActivity(
      new ImgurCommandParser(Configuration.ImgurCommandPrefix, Imgur),
      new ProcessTagCommandActivity(
       Imgur, Discord, RepositoryTaglistsCacheing
      )
     )
    ),
    Imgur, Discord, RepositoryTaglists, RepositorySettings
   );
   //Create Discord logging output if configured to do so
   Log.Bootstrap_.LogVerbose("Applying Configuration: Logging output");
   if(Configuration.LogToDiscord){
    Log.Instance.AddTraceListener(
     new DiscordTraceListener("DiscordListener", Discord, new TextWriterTraceListener(Console.Out))
    );
    Log.Instance.RemoveTraceListener("StdOutListener");
   }
   Log.Bootstrap_.LogInfo("Configuration applied; starting application");
   //Execution will be within this method while the application is running
   bool startupsuccess=Application.Run();
   Log.Bootstrap_.LogInfo("Application ended");
   if(!startupsuccess){
    return Return_ApplicationStartError;
   }
   return 0;
  }

  /// <summary>
  /// See <see cref="_Main"/>.
  /// The application optionally takes a single parameter from the command line,
  /// which is the path to Settings.xml,
  /// or more generally, a path to an XML instance document file of the Settings.xsd XML schema.
  /// If not specified, the path <see cref="DefaultSettingsFilePath"/> will be used.
  /// </summary>
  static int Main(string[] Parameters){
   return _Main(
    Parameters.Length>0 ? Parameters[0] : null
   );
  }

  /// <summary>
  /// The location where the Settings file Settings.xml resides,
  /// relative to the current working directory.
  /// </summary>
  public const string DefaultSettingsFilePath=@"Settings.xml";
  public const int Return_ConfigurationLoadError=-2;
  public const int Return_ApplicationStartError=-1;
 }
}