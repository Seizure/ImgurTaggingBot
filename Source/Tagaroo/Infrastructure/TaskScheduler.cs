using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tagaroo.Infrastructure{
 public class TaskScheduler{
  private readonly ICollection<ScheduledTask> Tasks=new List<ScheduledTask>();
  private List<Task> RunningTasks = null;
  private CancellationTokenSource StopSignal = null;
  private bool running=false;
  public TaskScheduler(){}

  public void AddTask(ScheduledTask Add){
   if(running){
    throw new InvalidOperationException("Cannot modify scheduled tasks while the scheduler is running");
   }
   this.Tasks.Add(Add);
  }

  public void ClearTasks(){
   if(running){
    throw new InvalidOperationException("Cannot modify scheduled tasks while the scheduler is running");
   }
   this.Tasks.Clear();
  }

  public async Task Run(){
   if(running){throw new InvalidOperationException("Already running");}
   this.StopSignal=new CancellationTokenSource();
   this.RunningTasks=new List<Task>(Tasks.Count);
   this.running=true;
   try{
    foreach(ScheduledTask thisTask in Tasks){
     Task RunningTask = RunTask(thisTask);
     this.RunningTasks.Add(RunningTask);
    }
    await Task.WhenAll( RunningTasks.ToArray() );
   }finally{
    this.StopSignal=null;
    this.RunningTasks=null;
    this.running=false;
   }
  }

  private async Task RunTask(ScheduledTask ToRun){
   if(ToRun.RunImmediately){
    await ToRun.Execute();
   }
   while(true){
    try{
     await Task.Delay(ToRun.Interval, StopSignal.Token);
    }catch(TaskCanceledException){
     break;
    }
    await ToRun.Execute();
   }
  }

  public void Stop(){
   if(!running){return;}
   StopSignal.Cancel();
  }
 }

 public class ScheduledTask{
  public TimeSpan Interval{get;}
  public Func<Task> Execute{get;}
  public bool RunImmediately{get;}
  protected ScheduledTask(TimeSpan Interval,Func<Task> Execute,bool RunImmediately){
   this.Interval=Interval;
   this.Execute=Execute;
   this.RunImmediately=RunImmediately;
  }

  static public ScheduledTask NewLaterTask(TimeSpan Interval,Func<Task> Execute){
   return new ScheduledTask(Interval,Execute,false);
  }
  static public ScheduledTask NewImmediateTask(TimeSpan Interval,Func<Task> Execute){
   return new ScheduledTask(Interval,Execute,true);
  }
 }
}