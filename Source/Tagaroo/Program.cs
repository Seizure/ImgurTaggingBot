﻿using System;
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
 internal class Program{
  public Program(){}

  public int Main(){
   Log.Instance.AddTraceListener(new TextWriterTraceListener(Console.Out,"StdOutListener"));
   Log.Instance.BootstrapLevel.Level = SourceLevels.Verbose;
   Log.Bootstrap_.LogInfo("Application starting");
   CultureInfo.CurrentCulture = CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
   SettingsRepository RepositorySettings=new SettingsRepositoryMain(SettingsFilePath);
   Log.Bootstrap_.LogVerbose("Initializing Settings repository");
   RepositorySettings.Initialize();
   Log.Bootstrap_.LogInfo("Loading Configuration");
   ApplicationConfiguration Configuration;
   try{
    Configuration = RepositorySettings.LoadConfiguration().Result;
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
   DiscordInterfacer Discord;
   ImgurInterfacer Imgur;
   CoreProcess Application=new CoreProcess(
    Imgur=new ImgurInterfacerMain(
     RepositorySettings,
     Configuration.ImgurClientID,
     Configuration.ImgurClientSecret,
     Configuration.ImgurUserAccountUsername,
     Configuration.ImgurUserAccountID,
     Configuration.ImgurOAuthAccessToken,
     Configuration.ImgurOAuthRefreshToken,
     Configuration.ImgurOAuthTokenType,
     Configuration.ImgurOAuthTokenExpiry,
     Configuration.ImgurMaximumCommentLengthUTF16CodeUnits
    ),
    Discord = new DiscordInterfacerMain(
     Configuration.DiscordAuthenticationToken,
     Configuration.DiscordGuildID,
     Configuration.DiscordChannelIDLog
    ),
    new TaglistRepositoryMain(
     Configuration.TaglistDataFilePath, true
    ),
    RepositorySettings,
    new ImgurCommandParser(Configuration.ImgurCommandPrefix, Imgur)
   );
   Log.Bootstrap_.LogVerbose("Applying Configuration: Logging output");
   if(Configuration.LogToDiscord){
    Log.Instance.AddTraceListener(
     new DiscordTraceListener("DiscordListener", Discord, new TextWriterTraceListener(Console.Out))
    );
    Log.Instance.RemoveTraceListener("StdOutListener");
   }
   Log.Bootstrap_.LogInfo("Configuration applied; starting application");
   bool startupsuccess=Application.Run();
   Log.Bootstrap_.LogInfo("Application ended");
   if(!startupsuccess){
    return Return_ApplicationStartError;
   }
   return 0;
  }

  static int Main(string[] Parameters){
   return new Program().Main();
  }

  const string SettingsFilePath=@"Settings.xml";
  public const int Return_ApplicationStartError=-1;
  public const int Return_ConfigurationLoadError=-2;
 }
}