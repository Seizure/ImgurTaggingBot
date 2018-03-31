using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tagaroo.Infrastructure{
 /// <summary>
 /// A read-write lock synchronization primitive for a single thread,
 /// allowing .NET TAP-based applications to synchronize access to a resource.
 /// Methods for acquiring locks will asynchronously block if the lock cannot yet be acquired;
 /// upon the <see cref="Task"/> completing the lock will have been acquired.
 /// Note this is not a multithreaded nor thread-safe synchronization primitive;
 /// consuming code is responsible for ensuring that
 /// there is only a single thread within one of its methods at any point in time.
 /// This assumes an appropriate synchronization context or other method
 /// to ensure continuations from awaits are not run while another thread is also in a method of the object,
 /// and that the continuations do not run concurrently.
 /// </summary>
 public class SingleThreadReadWriteLock{
  private ushort ReadLocks=0;
  private bool WriteLock=false;
  private TaskCompletionSource<object> ReadWaiter=null;
  private TaskCompletionSource<object> WriteWaiter=null;
  public SingleThreadReadWriteLock(){}

  /// <summary>
  /// Acquires a read lock.
  /// Will asynchronously block while a write lock is held.
  /// Any number of read locks are permitted.
  /// </summary>
  public async Task EnterReadLock(){
   while( WriteLock ){
    await ReadWaiter.Task;
   }
   if(ReadLocks <= 0){
    this.WriteWaiter = CreateWaiter();
   }
   ++ this.ReadLocks;
  }

  /// <summary>
  /// <para>Preconditions: Must be matched by an earlier call to <see cref="EnterReadLock"/></para>
  /// Releases a read lock.
  /// </summary>
  public void ExitReadLock(){
   if(ReadLocks<=0){throw new InvalidOperationException("Unmatched call to ExitReadLock");}
   -- this.ReadLocks;
   if(ReadLocks <= 0){
    this.WriteWaiter.SetResult(null);
    this.WriteWaiter = null;
   }
  }

  /// <summary>
  /// Acquires a write lock.
  /// Will asynchronously block while any read locks are held, and also if a write lock is held.
  /// Only a single write lock is permitted.
  /// </summary>
  public async Task EnterWriteLock(){
   while( ReadLocks > 0 || WriteLock){
    await WriteWaiter.Task;
   }
   this.WriteLock = true;
   this.ReadWaiter = CreateWaiter();
   this.WriteWaiter = CreateWaiter();
  }

  /// <summary>
  /// <para>Preconditions: Must be matched by an earlier call to <see cref="EnterWriteLock"/></para>
  /// Releases a write lock.
  /// </summary>
  public void ExitWriteLock(){
   if(!WriteLock){throw new InvalidOperationException("Unmatched call to ExitWriteLock");}
   this.WriteLock = false;
   this.ReadWaiter.SetResult(null);
   this.WriteWaiter.SetResult(null);
   this.ReadWaiter = null;
   this.WriteWaiter = null;
  }

  protected TaskCompletionSource<object> CreateWaiter(){
   return new TaskCompletionSource<object>(
    //Prevent reentrancy when *Waiter.SetResult is called
    TaskCreationOptions.RunContinuationsAsynchronously
   );
  }
 }
}