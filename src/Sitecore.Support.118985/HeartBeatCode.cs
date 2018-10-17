namespace Sitecore.Support.Web.Services
{
  using Sitecore.Configuration.Services;
  using Sitecore.Diagnostics;
  using Sitecore.StringExtensions;
  using Sitecore.Web.Services;
  using Sitecore.Web.Services.Heartbeat;
  using System;
  using System.Collections.Generic;
  using System.Configuration;
  using System.Data.SqlClient;
  using System.Linq;
  using System.Text.RegularExpressions;

  public class HeartbeatCode : Sitecore.Web.Services.HeartbeatCode
  {
    private void CheckDatabase(string connectionString)
    {
      Assert.IsNotNullOrEmpty(connectionString, "connectionString");
      if (!connectionString.StartsWith("mongodb://"))
      {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
          SqlCommand command = new SqlCommand("select * from sys.tables where 1=2", connection)
          {
            CommandTimeout = 1
          };
          using (SqlCommand command2 = command)
          {
            connection.Open();
            command2.ExecuteNonQuery();
          }
        }
      }
    }

    protected void CheckDatabases(BeatResults beatResults)
    {
      Assert.IsNotNull(beatResults, "beatResults");
      IEnumerable<string> excludeConnection = Heartbeat.ExcludeConnection;
      foreach (ConnectionStringSettings settings in ConfigurationManager.ConnectionStrings)
      {
        try
        {
          if (!excludeConnection.Contains<string>(settings.Name, StringComparer.InvariantCultureIgnoreCase))
          {
            CheckDatabase(settings.ConnectionString);
          }
        }
        catch (Exception exception)
        {
          SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings[settings.Name].ConnectionString);
          beatResults.Errors.Add(new Exception("Database {0} is not available".FormatWith(new object[] { Regex.Replace(settings.ConnectionString, "Password=" + builder.Password, "Password=*****", RegexOptions.IgnoreCase) }), exception));         
        }
      }
    }

    protected override BeatResults DoBeat()
    {
      BeatResults beatResults = new BeatResults();
      CheckDatabases(beatResults);
      return beatResults;
    }

    protected void Page_Load(object sender, EventArgs e)
    {
      Assert.IsNotNull(sender, "sender");
      Assert.IsNotNull(e, "e");
      BeatResults results = this.DoBeat();
      foreach (string str in results.Warnings)
      {
        Log.Warn("Sitecore heartbeat: " + str, this);
      }
      if (!results.IsOk)
      {
        foreach (Exception exception in results.Errors)
        {
          Log.Fatal("Sitecore heartbeat: ", exception, this);
        }
        base.Response.StatusCode = 500;
        base.Response.End();
      }
    }
  }
}