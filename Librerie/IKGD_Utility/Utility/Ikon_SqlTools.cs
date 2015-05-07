using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using System.Data;
using System.Data.SqlClient;
using LinqKit;

using Ikon;
using System.Data.Common;


namespace Ikon.SqlTools
{


  //
  // helper class per la generazione/creazione di tabelle
  // a partire da una DataTable
  //
  public class SqlTableCreator
  {
    private SqlConnection _connection;
    public SqlConnection Connection
    {
      get { return _connection; }
      set { _connection = value; }
    }

    private SqlTransaction _transaction;
    public SqlTransaction Transaction
    {
      get { return _transaction; }
      set { _transaction = value; }
    }

    private string _tableName;
    public string DestinationTableName
    {
      get { return _tableName; }
      set { _tableName = value; }
    }

    public SqlTableCreator() { }
    public SqlTableCreator(SqlConnection connection) : this(connection, null) { }
    public SqlTableCreator(SqlConnection connection, SqlTransaction transaction)
    {
      _connection = connection;
      _transaction = transaction;
    }

    public object Create(DataTable schema)
    {
      return Create(schema, null);
    }
    public object Create(DataTable schema, int numKeys)
    {
      int[] primaryKeys = new int[numKeys];
      for (int i = 0; i < numKeys; i++)
      {
        primaryKeys[i] = i;
      }
      return Create(schema, primaryKeys);
    }
    public object Create(DataTable schema, int[] primaryKeys)
    {
      string sql = GetCreateSQL(_tableName, schema, primaryKeys);

      SqlCommand cmd;
      if (_transaction != null && _transaction.Connection != null)
        cmd = new SqlCommand(sql, _connection, _transaction);
      else
        cmd = new SqlCommand(sql, _connection);

      return cmd.ExecuteNonQuery();
    }

    public object CreateFromDataTable(DataTable table)
    {
      string sql = GetCreateFromDataTableSQL(_tableName, table);

      SqlCommand cmd;
      if (_transaction != null && _transaction.Connection != null)
        cmd = new SqlCommand(sql, _connection, _transaction);
      else
        cmd = new SqlCommand(sql, _connection);

      return cmd.ExecuteNonQuery();
    }


    public static string GetCreateSQL(string tableName, DataTable schema, int[] primaryKeys)
    {
      string sql = "CREATE TABLE [" + tableName + "] (\n";

      // columns
      foreach (DataRow column in schema.Rows)
      {
        if (!(schema.Columns.Contains("IsHidden") && (bool)column["IsHidden"]))
        {
          sql += "\t[" + column["ColumnName"].ToString() + "] " + SQLGetType(column);

          if (schema.Columns.Contains("AllowDBNull") && (bool)column["AllowDBNull"] == false)
            sql += " NOT NULL";

          sql += ",\n";
        }
      }
      sql = sql.TrimEnd(new char[] { ',', '\n' }) + "\n";

      // primary keys
      string pk = ", CONSTRAINT PK_" + tableName + " PRIMARY KEY CLUSTERED (";
      bool hasKeys = (primaryKeys != null && primaryKeys.Length > 0);
      if (hasKeys)
      {
        // user defined keys
        foreach (int key in primaryKeys)
        {
          pk += schema.Rows[key]["ColumnName"].ToString() + ", ";
        }
      }
      else
      {
        // check schema for keys
        string keys = string.Join(", ", GetPrimaryKeys(schema));
        pk += keys;
        hasKeys = keys.Length > 0;
      }
      pk = pk.TrimEnd(new char[] { ',', ' ', '\n' }) + ")\n";
      if (hasKeys) sql += pk;

      sql += ")";

      return sql;
    }

    public static string GetCreateFromDataTableSQL(string tableName, DataTable table)
    {
      string sql = "CREATE TABLE [" + tableName + "] (\n";
      // columns
      foreach (DataColumn column in table.Columns)
      {
        sql += "[" + column.ColumnName + "] " + SQLGetType(column) + ",\n";
      }
      sql = sql.TrimEnd(new char[] { ',', '\n' }) + "\n";
      // primary keys
      if (table.PrimaryKey.Length > 0)
      {
        sql += "CONSTRAINT [PK_" + tableName + "] PRIMARY KEY CLUSTERED (";
        foreach (DataColumn column in table.PrimaryKey)
        {
          sql += "[" + column.ColumnName + "],";
        }
        sql = sql.TrimEnd(new char[] { ',' }) + "))\n";
      }

      //if not ends with ")"
      if ((table.PrimaryKey.Length == 0) && (!sql.EndsWith(")")))
      {
        sql += ")";
      }

      return sql;
    }

    public static string[] GetPrimaryKeys(DataTable schema)
    {
      List<string> keys = new List<string>();

      foreach (DataRow column in schema.Rows)
      {
        if (schema.Columns.Contains("IsKey") && (bool)column["IsKey"])
          keys.Add(column["ColumnName"].ToString());
      }

      return keys.ToArray();
    }

    // Return T-SQL data type definition, based on schema definition for a column
    public static string SQLGetType(object type, int columnSize, int numericPrecision, int numericScale)
    {
      switch (type.ToString())
      {
        case "System.String":
          return "VARCHAR(" + ((columnSize == -1) ? "255" : (columnSize > 8000) ? "MAX" : columnSize.ToString()) + ")";

        case "System.Data.SqlTypes.SqlString":
          return "NVARCHAR(" + ((columnSize == -1) ? "255" : (columnSize > 4000) ? "MAX" : columnSize.ToString()) + ")";

        case "System.Decimal":
          if (numericScale > 0)
            return "REAL";
          else if (numericPrecision > 10)
            return "BIGINT";
          else
            return "INT";

        case "System.Double":
        case "System.Single":
          return "REAL";

        case "System.Int64":
          return "BIGINT";

        case "System.Int16":
        case "System.Int32":
          return "INT";

        case "System.DateTime":
          return "DATETIME";

        case "System.Boolean":
          return "BIT";

        case "System.Byte":
          return "TINYINT";

        case "System.Guid":
          return "UNIQUEIDENTIFIER";

        default:
          throw new Exception(type.ToString() + " not implemented.");
      }
    }

    // Overload based on row from schema table
    public static string SQLGetType(DataRow schemaRow)
    {
      return SQLGetType(schemaRow["DataType"],
                          int.Parse(schemaRow["ColumnSize"].ToString()),
                          int.Parse(schemaRow["NumericPrecision"].ToString()),
                          int.Parse(schemaRow["NumericScale"].ToString()));
    }
    // Overload based on DataColumn from DataTable type
    public static string SQLGetType(DataColumn column)
    {
      return SQLGetType(column.DataType, column.MaxLength, 10, 2);
    }


    //
    // creazione diretta di una tabella su SQL Server a partire da un xml
    //
    public int CreateAndImportTableFromXml(string tableName, XElement xData)
    {
      int rows = -1;
      if (string.IsNullOrEmpty(tableName) || Connection == null || xData == null || xData.Elements().Count() == 0)
        return rows;
      var fields = xData.Elements().SelectMany(x => x.Elements().Select(e => e.Name.LocalName)).Distinct().ToList();
      var fields2 = xData.Elements().SelectMany(x => x.Elements().Select(r => new { field = r.Name.LocalName, len = (r.Value ?? string.Empty).Length })).GroupBy(r => r.field).Select(g => new { field = g.Key, len = g.Max(r => r.len) }).ToList();
      rows = xData.Elements().Count();
      //
      if (rows > 0)
      {
        //
        DataTable dt = new DataTable(tableName);
        //
        foreach (var fld in fields2)
        {
          DataColumn col = new DataColumn(fld.field);
          var values = xData.Elements().Select(x => x.ElementValue(fld.field));
          col.AllowDBNull = values.Any(r => string.IsNullOrEmpty(r));
          if (values.All(r => r.DefaultIfEmptyTrim(null) == null))
          {
            col.DataType = typeof(string);
          }
          else if (values.Where(r => !string.IsNullOrEmpty(r)).All(r => Utility.TryParse<int?>(r).ToString() == r.Trim()))
          {
            col.DataType = typeof(int);
          }
          else if (values.Where(r => !string.IsNullOrEmpty(r)).All(r => { var d = Utility.TryParse<DateTime>(r, Utility.DateTimeMinValueDB); return Utility.DateTimeMinValueDB < d && d < Utility.DateTimeMaxValueDB; }))
          {
            col.DataType = typeof(DateTime);
          }
          else if (values.Where(r => !string.IsNullOrEmpty(r)).All(r => Encoding.UTF8.GetByteCount(r) == r.Length))
          {
            col.DataType = typeof(string);
            if (fld.len > 256)
              col.MaxLength = ((fld.len + 1023) / 1024) * 1024;
          }
          else
          {
            col.DataType = typeof(System.Data.SqlTypes.SqlString);
            if (fld.len > 256)
              col.MaxLength = ((fld.len + 1023) / 1024) * 1024;
          }
          dt.Columns.Add(col);
        }
        //
        var cols = dt.Columns.OfType<DataColumn>().ToList();
        //
        foreach (XElement xe in xData.Elements())
        {
          DataRow row = dt.NewRow();
          foreach (XElement x in xe.Elements())
          {
            if (dt.Columns[x.Name.LocalName].AllowDBNull && x.Value.DefaultIfEmptyTrim(null) == null)
              continue;
            row[x.Name.LocalName] = x.Value;
          }
          dt.Rows.Add(row);
        }
        //
        var tableSql = SqlTableCreator.GetCreateFromDataTableSQL(dt.TableName, dt);
        //
        try
        {
          SqlCommand cmd = new SqlCommand("DROP TABLE [{0}]".FormatString(dt.TableName), Connection);
          cmd.ExecuteNonQuery();
        }
        catch { }
        //
        SqlTableCreator tableCreator = new SqlTableCreator(Connection);
        tableCreator.DestinationTableName = dt.TableName;
        tableCreator.CreateFromDataTable(dt);
        //
        System.Data.SqlClient.SqlBulkCopy bulkCopy = new System.Data.SqlClient.SqlBulkCopy(Connection);
        try
        {
          bulkCopy.DestinationTableName = dt.TableName;
          bulkCopy.WriteToServer(dt);
        }
        finally
        {
          bulkCopy.Close();
        }
        //
        //SqlCommand insertCommand = new SqlCommand(string.Format("SELECT * INTO [{0}] FROM @tvp", tableName), Connection);
        //SqlParameter tvpParam = insertCommand.Parameters.AddWithValue("@tvp", dt);
        //tvpParam.SqlDbType = SqlDbType.Structured;
        //tvpParam.TypeName = "dbo.{0}".FormatString(tableName);
        //int res = insertCommand.ExecuteNonQuery();
        //
      }
      //
      return rows;
      //
    }



  }



}