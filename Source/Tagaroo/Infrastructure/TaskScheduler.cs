using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tagaroo.Infrastructure{
 /// <summary>
 /// Schedules tasks to execute at intervals, using .NET TAP.
 /// </summary>
 public class TaskScheduler{
  private readonly ICollection<ScheduledTask> Tasks=new List<ScheduledTask>();
  private List<Task> RunningTasks = null;
  private CancellationTokenSource StopSignal = null;
  private bool running=false;
  public TaskScheduler(){}

  /// <summary>
  /// <para>Preconditions: Scheduler is not running</para>
  /// Adds a task for the scheduler to execute at specified intervals.
  /// </summary>
  public void AddTask(ScheduledTask Add){
   if(running){
    throw new InvalidOperationException("Cannot modify scheduled tasks while the scheduler is running");
   }
   this.Tasks.Add(Add);
  }

  /// <summary>
  /// <para>Preconditions: Scheduler is not running</para>
  /// Removes all the tasks that have been added to the scheduler, effectively resetting it.
  /// </summary>
  public void ClearTasks(){
   if(running){
    throw new InvalidOperationException("Cannot modify scheduled tasks while the scheduler is running");
   }
   this.Tasks.Clear();
  }

  /// <summary>
  /// <para>
  /// Preconditions: Scheduler is not running;
  /// Postconditions (method return): Scheduler is running;
  /// Postconditions (Task): Scheduler is not running
  /// </para>
  /// Runs the scheduler, executing all tasks that have been added to it at their specified intervals.
  /// The returned Task will complete once <see cref="Stop"/> has been called,
  /// and any currently running tasks have completed.
  /// </summary>
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

  /// <summary>
  /// Signals for the scheduler to stop running.
  /// Note that the scheduler will probably still be running when this call completes;
  /// this method merely signals it to stop;
  /// the scheduler will have entered the stopped state when the Task returned by <see cref="Run"/> completes.
  /// </summary>
  public void Stop(){
   if(!running){return;}
   StopSignal.Cancel();
  }
 }

 /// <summary>
 /// Component class used by <see cref="TaskScheduler"/>.
 /// </summary>
 public class ScheduledTask{
  /// <summary>
  /// The interval at which to repeat execution of the task.
  /// </summary>
  public TimeSpan Interval{get;}
  /// <summary>
  /// What to execute.
  /// </summary>
  public Func<Task> Execute{get;}
  /// <summary>
  /// <para>Preconditions: <paramref name="Interval"/> is non-negative and not greater than <see cref="Int32.MaxValue"/> milliseconds</para>
  /// If true, the task will be executed once the scheduler starts, and then at the defined intervals.
  /// If false, the first execution of the task will be after its interval has passed.
  /// </summary>
  public bool RunImmediately{get;}
  protected ScheduledTask(TimeSpan Interval,Func<Task> Execute,bool RunImmediately){
   if(Interval < TimeSpan.Zero || Interval.TotalMilliseconds > Int32.MaxValue){
    throw new ArgumentOutOfRangeException(nameof(Interval));
   }
   this.Interval=Interval;
   this.Execute=Execute;
   this.RunImmediately=RunImmediately;
  }

  /// <summary>
  /// <para>Preconditions: <paramref name="Interval"/> is non-negative and not greater than <see cref="Int32.MaxValue"/> milliseconds</para>
  /// Creates a new task with <see cref="RunImmediately"/> as false.
  /// </summary>
  static public ScheduledTask NewLaterTask(TimeSpan Interval,Func<Task> Execute){
   return new ScheduledTask(Interval,Execute,false);
  }
  /// <summary>
  /// <para>Preconditions: <paramref name="Interval"/> is non-negative and not greater than <see cref="Int32.MaxValue"/> milliseconds</para>
  /// Creates a new task with <see cref="RunImmediately"/> as true.
  /// </summary>
  static public ScheduledTask NewImmediateTask(TimeSpan Interval,Func<Task> Execute){
   return new ScheduledTask(Interval,Execute,true);
  }
 }
}