using System;
using System.Collections.Generic;
using System.Text;

namespace Tagaroo.Application{
 /// <summary>
 /// Model class for settings of the application, which control various behaviors,
 /// and store information to be persisted across executions of the application.
 /// It is intended that many of these settings be changable at runtime,
 /// without having to restart the application.
 /// Instances of this class are typically retrieved from the <see cref="DataAccess.SettingsRepository"/>.
 /// </summary>
 public class Settings{
  
  private TimeSpan _PullCommentsFrequency = TimeSpan.FromHours(1);

  /// <summary>
  /// A collection of usernames of Imgur users who are authorized to issue Imgur commands to the application,
  /// such as Tagging items.
  /// The Imgur Comments made by these users will be scanned for commands.
  /// Writable as well as mutable.
  /// </summary>
  public ISet<string> CommenterUsernames{get;set;} = new HashSet<string>(0);

  /// <summary>
  /// The date–time of the most recent Imgur Comment that was processed by the application.
  /// The application updates and saves this value after processing one or more Comments.
  /// Storing this value allows the application to only retrieve a small subset
  /// of a Commenter's Comment history when looking for new Comments,
  /// the subset of Comments after this point in time.
  /// </summary>
  public DateTimeOffset CommentsProcessedUpToInclusive{get;set;} = DateTimeOffset.MinValue;
  
  /// <summary>
  /// A safety feature preventing the application from requesting huge amounts of historical Comment data,
  /// and consuming large amounts of limited API calls to the Imgur API.
  /// This is the maximum number of "pages" of Comments that the application may pull for an individual Commenter,
  /// for an individual Pull-Latest-Comments operation.
  /// A "page" typically consists of 50 Comments.
  /// If this limit is reached, a message is logged, and no further Comments are retrieved.
  /// Although not recommended, this safety feature can be disabled by setting its value to 0.
  /// </summary>
  public byte RequestThreshholdPullCommentsPerUser{get;set;} = 10;

  public Settings(){}

  /// <summary>
  /// Must be positive, and must not be greater than <see cref="Int32.MaxValue"/> milliseconds in length.
  /// Changes made to this value are not currently be persisted by <see cref="DataAccess.SettingsRepository.SaveWritableSettings"/>.
  /// The application will wait for this length of time between pulling the latest Comments from Imgur.
  /// A shorter delay will increase the amount of limited API calls the application makes to Imgur over time.
  /// </summary>
  public TimeSpan PullCommentsFrequency{
   get{return _PullCommentsFrequency;}
   set{
    if(value <= TimeSpan.Zero || value.TotalMilliseconds > Int32.MaxValue){
     throw new ArgumentOutOfRangeException();
    }
    this._PullCommentsFrequency=value;
   }
  }
 }
}