using System;

namespace Tagaroo.DataAccess{
 public class DataAccessException:Exception{
  public DataAccessException(string Message)
  :base(Message){}
  public DataAccessException(string Message,Exception Inner)
  :base(Message,Inner){}
 }
}