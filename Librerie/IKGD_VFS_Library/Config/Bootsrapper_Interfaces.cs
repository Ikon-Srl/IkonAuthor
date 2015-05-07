/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2012 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


namespace Ikon.IKCMS
{

  public interface IBootStrapperPreTask
  {
    void ExecutePre();
  }


  public interface IBootStrapperTask
  {
    void Execute();
  }


  public interface IBootStrapperPostTask
  {
    void ExecutePost();
  }

}

