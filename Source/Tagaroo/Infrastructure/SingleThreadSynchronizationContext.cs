using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Tagaroo.Infrastructure{
 /// <summary>
 /// <see cref="SynchronizationContext"/> Realization that synchronizes to the thread
 /// that calls the <see cref="RunOnCurrentThread"/> method.
 /// This Realization does not support the <see cref="SynchronizationContext.Send(SendOrPostCallback,object)"/> method.
 /// </summary>
 internal class SingleThreadSynchronizationContext : SynchronizationContext{
  private readonly BlockingCollection<Tuple<SendOrPostCallback,object>> MessageQueue
   = new BlockingCollection<Tuple<SendOrPostCallback, object>>( new ConcurrentQueue<Tuple<SendOrPostCallback, object>>() );
  private bool running=false;
  private int? RunningOnThreadID;
  
  public SingleThreadSynchronizationContext(){}

  public override void Post(SendOrPostCallback d,object state){
   Post(d,state,false);
  }
  
  /// <summary>
  /// As for <see cref="Post(SendOrPostCallback,object)"/>, but the call will always return before the callback is executed,
  /// even if the call is on the right thread.
  /// </summary>
  public void PostAsync(SendOrPostCallback d,object state){
   Post(d,state,true);
  }
  
  protected void Post(SendOrPostCallback d,object state,bool ForceAsync){
   if(MessageQueue.IsAddingCompleted){
    //TODO Better way of handling callbacks during shutdown
    return;
   }
   if(d is null){throw new ArgumentNullException(nameof(d));}
   if( Thread.CurrentThread.ManagedThreadId != RunningOnThreadID || ForceAsync ){
    MessageQueue.Add(new Tuple<SendOrPostCallback, object>(d,state));
   }else{
    //If we're already on the right thread, we can immediately execute the callback
    d(state);
   }
  }

  public override void Send(SendOrPostCallback d,object state){
   throw new NotSupportedException();
  }

  public void Finish(){
   if(!running){return;}
   if(MessageQueue.IsAddingCompleted){return;}
   MessageQueue.CompleteAdding();
  }

  /// <summary>
  /// <para>Preconditions: Not already running</para>
  /// Sets <see cref="SynchronizationContext.Current"/> to this synchronization context,
  /// and blocks until <see cref="Finish"/> is called,
  /// at which point <see cref="SynchronizationContext.Current"/> is reset to its previous value.
  /// </summary>
  public void RunOnCurrentThread(){
   if(running){throw new InvalidOperationException("Already running");}
   SynchronizationContext OriginalContext = SynchronizationContext.Current;
   this.RunningOnThreadID = Thread.CurrentThread.ManagedThreadId;
   this.running = true;
   try{
    SynchronizationContext.SetSynchronizationContext(this);
    while(MessageQueue.TryTake(
     out Tuple<SendOrPostCallback,object> Message,
     Timeout.Infinite
    )){
     Message.Item1(Message.Item2);
    }
   }finally{
    SynchronizationContext.SetSynchronizationContext(OriginalContext);
    this.RunningOnThreadID=null;
    this.running=false;
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
  /// As for <see cref="RunOnCurrentThread"/>, but begins by executing the supplied action
  /// once <see cref="SynchronizationContext.Current"/> has been set.
  /// </summary>
  public void RunOnCurrentThread(Action BeginWith){
   if(running){throw new InvalidOperationException("Already running");}
   PostAsync(
    _ => BeginWith(),
    null
   );
   RunOnCurrentThread();
  }
 }
}