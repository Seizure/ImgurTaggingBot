using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Tagaroo.Application{
 internal class SingleThreadSynchronizationContext : SynchronizationContext{
  private readonly BlockingCollection<Tuple<SendOrPostCallback,object>> MessageQueue
   = new BlockingCollection<Tuple<SendOrPostCallback, object>>( new ConcurrentQueue<Tuple<SendOrPostCallback, object>>() );
  private bool running=false;
  private int? RunningOnThreadID;
  
  public SingleThreadSynchronizationContext(){}

  public override void Post(SendOrPostCallback d,object state){
   Post(d,state,false);
  }
  public void PostAsync(SendOrPostCallback d,object state){
   Post(d,state,true);
  }
  
  protected void Post(SendOrPostCallback d,object state,bool ForceAsync){
   if(MessageQueue.IsAddingCompleted){
    //TODO Better way of handling callbacks during shutdown
    return;
   }
   if(d==null){throw new ArgumentNullException(nameof(d));}
   if( Thread.CurrentThread.ManagedThreadId != RunningOnThreadID || ForceAsync ){
    MessageQueue.Add(new Tuple<SendOrPostCallback, object>(d,state));
   }else{
    //Can immediately execute callback if already on the right thread
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
  (there is a potential unobserved exception in CoreProcess.ProcessTagCommand).
  Otherwise, treating the function as async Task without this directive,
  will cause any unhandled exceptions to go unnoticed,
  thus treat as async void (Action) to ensure unhandled exceptions don't go unnoticed.
  */
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