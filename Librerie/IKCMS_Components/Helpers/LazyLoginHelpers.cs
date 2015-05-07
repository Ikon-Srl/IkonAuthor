using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Transactions;
using Autofac;
//using Microsoft.Web.Mvc;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS
{
  public enum CartTypeEnum { CartActive = 0, WishList = 1, CartArchive = 2, CartCheckout = 3 };


  public static class LazyLoginHelperExtensions
  {
    // standardized DB accesor for client custom tables
    public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //

    public static bool RegisterVote(int rNode, int? Category, int? voteValue, string voteText) { return RegisterVote(null, rNode, Category, voteValue, voteText); }
    public static bool RegisterVote(ILazyLoginMapper lazyLoginMapper, int rNode, int? Category, int? voteValue, string voteText)
    {
      try
      {
        Category = Category ?? 0;
        lazyLoginMapper = lazyLoginMapper ?? MembershipHelper.LazyLoginMapperObject;
        var voteRecord = fsOp.DB.LazyLogin_Votes.FirstOrDefault(r => r.rNode == rNode && r.IdLL == lazyLoginMapper.Id && r.Category == Category.Value);
        if (voteRecord == null)
        {
          voteRecord = new LazyLogin_Vote() { IdLL = lazyLoginMapper.Id, rNode = rNode, Category = Category.Value };
          voteRecord.InitializeLL();
          fsOp.DB.LazyLogin_Votes.InsertOnSubmit(voteRecord);
        }
        //
        if (voteValue != null)
          voteRecord.Value = voteValue;
        if (voteText != null)
          voteRecord.Text = voteText;
        //
        fsOp.DB.SubmitChanges();
        //
        return true;
      }
      catch { }
      return false;
    }

  }




  //
  // migrazione dei dati relativi alle estensioni Lazylogin per i carrelli
  //
  /*
  public class IKGD_MembershipAnonymousDataMigration_LazyLoginIKCMS : I_IKGD_MembershipAnonymousDataMigration
  {
    public static int MigrateAnonymousData(IKGD_DataContext DB, MembershipUser userOld, ILazyLoginMapper UserLL_Old, MembershipUser userNew, ILazyLoginMapper UserLL_New)
    {
      using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
      {
        try
        {
          if (UserLL_Old != null && UserLL_New != null)
          {
            int res01 = DB.ExecuteCommand("UPDATE [LazyLogin_CartMain] SET IdLL={1} WHERE (IdLL={0})", UserLL_Old.Id, UserLL_New.Id);
            int CartId_WL = DB.GetScalarValue<int>("SELECT CartId FROM [LazyLogin_CartMain] WHERE (IdLL={0} AND [Type]='WishList') ORDER BY [Creat] DESC", UserLL_New.Id);
            int CartId_CA = DB.GetScalarValue<int>("SELECT CartId FROM [LazyLogin_CartMain] WHERE (IdLL={0} AND [Type]='CartActive') ORDER BY [Creat] DESC", UserLL_New.Id);
            //int res02 = DB.ExecuteCommand("UPDATE [LazyLogin_CartMain] SET [Type]='CartArchive' WHERE (CartId<>{0} AND ...)", CartId_WL);
            //int res03 = DB.ExecuteCommand("UPDATE [LazyLogin_CartMain] SET [Type]='CartArchive' WHERE (CartId<>{0})", CartId_CA);
            // TODO: completare la mgrazione dei carrelli
          }
          //
          ts.Complete();
        }
        catch { }
      }
      return 0;
    }
  }
  */

}
