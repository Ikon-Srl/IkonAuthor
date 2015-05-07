using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;


namespace LINQhelper
{
  using Ikon;


  public static class DataTableHelper
  {
    // Helper function for ADO.Net Bulkcopy to transfer a IEnumerable list to a datatable
    // Adapted from: http://msdn.microsoft.com/en-us/library/bb396189.aspx
    public static DataTable CopyToDataTable<T>(IEnumerable<T> source)
    {
      return new DataTableCreator<T>().CreateDataTable(source, null, null);
    }


    public static DataTable CopyToDataTable<T>(IEnumerable<T> source, DataTable table, LoadOption? options)
    {
      return new DataTableCreator<T>().CreateDataTable(source, table, options);
    }


    public static bool BulkCopyToDatabase<T>(IEnumerable<T> source, System.Data.Linq.DataContext databaseContext) where T : class
    {
      try
      {
        var attr = typeof(T).GetCustomAttributes(typeof(System.Data.Linq.Mapping.TableAttribute), true).FirstOrDefault() as System.Data.Linq.Mapping.TableAttribute;
        if (attr != null && attr.Name.IsNotEmpty())
        {
          BulkCopyToDatabase(source, attr.Name, databaseContext);
          return true;
        }
      }
      catch { }
      return false;
    }


    public static void BulkCopyToDatabase<T>(IEnumerable<T> source, string tableName, System.Data.Linq.DataContext databaseContext) where T : class
    {
      using (var dataTable = DataTableHelper.CopyToDataTable(source))
      {
        using (var bulkCopy = new SqlBulkCopy(databaseContext.Connection.ConnectionString, SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.KeepNulls))
        {
          foreach (DataColumn dc in dataTable.Columns)
            bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(dc.ColumnName, dc.ColumnName));
          var tableNameNorm = tableName.Trim(" []".ToCharArray());
          if (!tableNameNorm.StartsWith("dbo.", StringComparison.OrdinalIgnoreCase))
            tableNameNorm = string.Format("[{0}]", tableNameNorm);
          bulkCopy.DestinationTableName = tableName;
          bulkCopy.WriteToServer(dataTable);
        }
      }
    }


  }
}
