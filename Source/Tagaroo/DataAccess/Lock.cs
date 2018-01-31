using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Tagaroo.DataAccess{
 public interface Lock : IDisposable{
  void Release();
 }

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