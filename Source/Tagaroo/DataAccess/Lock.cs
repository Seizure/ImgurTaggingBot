using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Tagaroo.DataAccess{
 /// <summary>
 /// Represents a Pessimistic Lock held on some entity or set of entities,
 /// preventing concurrent updates to those entities from being saved,
 /// which would otherwise result in concurrency faults such as lost updates.
 /// Until the lock is released, nothing else will be able to persist modifications
 /// to the entity or entities that the lock is associated with.
 /// </summary>
 public interface Lock : IDisposable{
  /// <summary>
  /// Releases the lock, or does nothing if the lock is already released.
  /// After this call, it will be possible for other things to persist modifications made
  /// to the entity or entities that this lock was associated with.
  /// Implementations should call this method from the <see cref="IDisposable.Dispose"/> method.
  /// </summary>
  void Release();
 }

 /// <summary>
 /// Realization of <see cref="Lock"/>, implementing a Pessimistic Lock for file-based data stores.
 /// Works simply by holding on to an already-open file handle, closing it when released.
 /// The file handle passed to it should have been opened with the appropriate
 /// <see cref="System.IO.FileAccess"/> and <see cref="System.IO.FileShare"/> values.
 /// </summary>
 internal class FileLock : Lock{
  private readonly Stream LockedFile;
  public FileLock(Stream LockedFile){
   this.LockedFile=LockedFile;
  }

  public void Release(){
   LockedFile.Close();
  }

  void IDisposable.Dispose(){
   Release();
  }
 }
}