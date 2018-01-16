using System;
using System.Collections.Generic;
using System.Text;

namespace Tagaroo.Application{
 public class Settings{
  
  private TimeSpan _PullCommentsFrequency = TimeSpan.FromHours(1);

  public ISet<string> CommenterUsernames{get;set;} = new HashSet<string>(0);

  public DateTimeOffset CommentsProcessedUpToInclusive{get;set;} = DateTimeOffset.MinValue;
  
  public byte RequestThreshholdPullCommentsPerUser{get;set;} = 10;

  public Settings(){}

  /*
  public TimeSpan PullCommentsFrequency{
   get{return _PullCommentsFrequency;}
   set{
    if(value<=TimeSpan.Zero){throw new ArgumentOutOfRangeException();}
    this._PullCommentsFrequency=value;
   }
  }
  */
 }
}