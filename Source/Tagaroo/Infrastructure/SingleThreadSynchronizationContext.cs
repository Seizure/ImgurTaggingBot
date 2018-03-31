using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Tagaroo.Infrastructure{
 /// <summary>
 /// <see cref="SynchronizationContext"/> realization that synchronizes to the thread
 /// that calls the <see cref="RunOnCurrentThread"/> method.
 /// Any calls made to <see cref="Post"/> before <see cref="RunOnCurrentThread"/> is called will be delayed until it is called;
 /// any calls made to <see cref="Post"/> once <see cref="Finish"/> has been called
 /// are sent to the finishing synchronization context supplied in the constructor.
 /// This Realization does not support the <see cref="SynchronizationContext.Send(SendOrPostCallback,object)"/> method.
 /// </summary>
 internal class SingleThreadSynchronizationContext : SynchronizationContext{
  private readonly BlockingCollection<Tuple<SendOrPostCallback,object>> MessageQueue
   = new BlockingCollection<Tuple<SendOrPostCallback, object>>( new ConcurrentQueue<Tuple<SendOrPostCallback, object>>() );
  private readonly bool AllowSynchronousExecution;
  private bool running=false;
  private int? RunningOnThreadID=null;
  private SynchronizationContext FinishedContext;
  
  public SingleThreadSynchronizationContext(SynchronizationContext FinishedContext)
  :this(FinishedContext,Options.None){}
  /// <param name="FinishedContext">
  /// The <see cref="SynchronizationContext"/> to call
  /// upon any calls to <see cref="Post"/> after <see cref="Finish"/> has been called
  /// </param>
  public SingleThreadSynchronizationContext(SynchronizationContext FinishedContext,Options Options){
   if(FinishedContext is null){throw new ArgumentNullException();}
   this.AllowSynchronousExecution = !((Options&Options.NoSynchronousExecution)!=Options.None);
   this.FinishedContext=FinishedContext;
  }

  public override void Post(SendOrPostCallback d,object state){
   Post(d,state,!AllowSynchronousExecution);
  }
  
  /// <summary>
  /// As for <see cref="Post(SendOrPostCallback,object)"/>, but the call will always return before the callback is executed,
  /// even if the call is on the right thread.
  /// </summary>
  public void PostAsync(SendOrPostCallback d,object state){
   Post(d,state,true);
  }
  
  protected void Post(SendOrPostCallback d,object state,bool ForceAsync){
   lock(MessageQueue){
    if(MessageQueue.IsAddingCompleted){
     FinishedContext.Post(d,state);
     return;
    }
    if(d is null){throw new ArgumentNullException(nameof(d));}
    if( !running || Thread.CurrentThread.ManagedThreadId != RunningOnThreadID || ForceAsync ){
     MessageQueue.Add(new Tuple<SendOrPostCallback, object>(d,state));
    }else{
     //Optimization — If we're already on the right thread, we can immediately execute the callback
     d(state);
    }
   }
  }

  public override void Send(SendOrPostCallback d,object state){
   throw new NotSupportedException();
  }

  public void Finish(){
   lock(MessageQueue){
    if(!running){return;}
    if(MessageQueue.IsAddingCompleted){return;}
    MessageQueue.CompleteAdding();
   }
  }

  /// <summary>
  /// <para>Preconditions: Not already running</para>
  /// Callers should usually call <see cref="SynchronizationContext.SetSynchronizationContext"/> with this object
  /// before calling this method.
  /// Blocks until <see cref="Finish"/> is called,
  /// synchronizing all calls synchronized to this context to the thread on which this method was called.
  /// Once finish is called, any further attempts to synchronize calls
  /// will be synchronized to the finishing <see cref="SynchronizationContext"/> specified when constructing this object.
  /// </summary>
  public void RunOnCurrentThread(){
   if(running){throw new InvalidOperationException("Already running");}
   this.RunningOnThreadID = Thread.CurrentThread.ManagedThreadId;
   this.running = true;
   try{
    //SynchronizationContext.SetSynchronizationContext(this);
    while(MessageQueue.TryTake(
     out Tuple<SendOrPostCallback,object> Message,
     Timeout.Infinite
    )){
     Message.Item1(Message.Item2);
    }
   }finally{
    lock(MessageQueue){
     //SynchronizationContext.SetSynchronizationContext(OriginalContext);
     this.RunningOnThreadID=null;
     this.running=false;
    }
   }
  }

  /*
  Any async function passed to this method should be treated as async void (Action), and not async Task (Func<Task>).
  Unhandled exceptions thrown from async void methods will cause the application to crash, which is desirable,
  whereas unhandled exceptions from async Task methods, where the returned Task is never awaited, are swallowed.
  This swallowing may be disabled by enabling the ThrowUnobservedTaskExceptions configuration directive,
  although this will cause the application to crash for any unobserved exception, which may not be desirable
  (there are a few potential unobserved exceptions in the application).
  Otherwise, treating the function as async Task without this directive,
  will cause any unhandled exceptions to go unnoticed,
  thus treat as async void (Action) to ensure unhandled exceptions don't go unnoticed.
  */
  /// <summary>
  /// As for <see cref="RunOnCurrentThread"/>,
  /// but first posts the supplied action to this synchronization context,
  /// which will be executed when the synchronization context then starts up.
  /// </summary>
  public void RunOnCurrentThread(Action BeginWith){
   if(running){throw new InvalidOperationException("Already running");}
   PostAsync(
    _ => BeginWith(),
    null
   );
   RunOnCurrentThread();
  }

  [Flags]
  public enum Options{
   None=0x00,
   /// <summary>
   /// By default, delegates passed in calls to <see cref="Post"/> have their execution optimized,
   /// in that if the caller is already on the right thread, they are executed synchronously,
   /// within the call to <see cref="Post"/>.
   /// Specify this option when constructing a <see cref="SingleThreadSynchronizationContext"/>
   /// to disable this optimization, in which case <see cref="Post"/> will behave as <see cref="PostAsync"/>.
   /// </summary>
   NoSynchronousExecution=0x01
  }
 }

 /// <summary>
 /// A <see cref="SynchronizationContext"/> realization that does nothing with the delegates passed to it;
 /// no delegates passed to it will get called.
 /// This should typically only be used during application shutdown.
 /// </summary>
 internal class NullSynchronizationContext : SynchronizationContext{
  public NullSynchronizationContext(){}
  public override void Post(SendOrPostCallback d,object state){
   return;
  }
  public override void Send(SendOrPostCallback d,object state){
   return;
  }
 }
}