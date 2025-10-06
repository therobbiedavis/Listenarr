using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Models
{
    public static class SqlitePragmaInitializer
    {
        public static void ApplyPragmas(DbContext context)
        {
            var conn = context.Database.GetDbConnection();
            if (conn is SqliteConnection sqliteConn)
            {
                if (sqliteConn.State != System.Data.ConnectionState.Open)
                {
                    sqliteConn.Open();
                }
                
                using var cmd = sqliteConn.CreateCommand();
                cmd.CommandText = @"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA journal_size_limit=6144000;";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
