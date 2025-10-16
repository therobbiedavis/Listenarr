using System;
using Microsoft.Data.Sqlite;
using System.IO;

static class DeleteDownloadsProgram
{
    // Helper to delete all rows in the Downloads table. Use DeleteDownloadsProgram.Run()
    // from a separate tool if you need to execute this logic. This avoids emitting
    // a program entry point (Main) when the web project uses top-level statements.
    public static int Run()
    {
        try
        {
            string dbPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "database", "listenarr.db");
            if (!File.Exists(dbPath))
            {
                Console.WriteLine($"Database file not found at: {dbPath}");
                return 2;
            }

            Console.WriteLine($"Opening database: {dbPath}");

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            using var tx = connection.BeginTransaction();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM \"Downloads\";";
                cmd.ExecuteNonQuery();
                Console.WriteLine("Deleted rows from Downloads table.");
            }

            // Reset sqlite_sequence for Downloads if present
            using (var cmd2 = connection.CreateCommand())
            {
                cmd2.CommandText = "DELETE FROM sqlite_sequence WHERE name='Downloads';";
                try
                {
                    cmd2.ExecuteNonQuery();
                    Console.WriteLine("Reset sqlite_sequence for Downloads (if it existed).");
                }
                catch { }
            }

            tx.Commit();
            connection.Close();
            Console.WriteLine("Done.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
