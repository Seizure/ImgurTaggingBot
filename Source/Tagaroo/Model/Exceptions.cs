using System;

namespace Tagaroo.Model{
 public class EntityNotFoundException:Exception{
  public EntityNotFoundException()
  :base(){}
 }

 public class AlreadyExistsException:Exception{
  public AlreadyExistsException()
  :base(){}
  public AlreadyExistsException(string Message)
  :base(Message){}
 }
}