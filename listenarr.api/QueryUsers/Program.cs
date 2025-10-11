// See https://aka.ms/new-console-template for more information
using System;
using Microsoft.Data.Sqlite;

partial class Program
{
    // Utility method for querying users. Renamed from Main to avoid emitting a second
    // program entry point when the web project uses top-level statements.
    public static void Run()
    {
        try
        {
            // Path to the database file
            string dbPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "listenarr.db");
            Console.WriteLine($"Database path: {dbPath}");

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Username, Email, IsAdmin, CreatedAt FROM Users";

            using var reader = command.ExecuteReader();
            Console.WriteLine("\nUsers in database:");
            Console.WriteLine("ID | Username | Email | IsAdmin | CreatedAt");
            Console.WriteLine("---|----------|-------|---------|-----------");

            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var username = reader.GetString(1);
                var email = reader.IsDBNull(2) ? "NULL" : reader.GetString(2);
                var isAdmin = reader.GetBoolean(3);
                var createdAt = reader.GetDateTime(4);

                Console.WriteLine($"{id} | {username} | {email} | {isAdmin} | {createdAt}");
            }

            if (!reader.HasRows)
            {
                Console.WriteLine("No users found in database.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
