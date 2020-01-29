namespace IsolatedCacheIssue
{
    using System;
    using System.Data.SQLite;

    internal class Program
    {
        private static readonly string DatabaseFileName = "Simple.db";

        // Adding one of the following parameters to connection string resolves the issue:
        //      ;journal mode=Wal
        //      ;Cache Size=0
        // Removing or simplifying password also resolves the issue:
        //      ;password=12345678901        - No issue
        //      ;password=123456789012       - Will be reproduced
        //      ;password=00000000000000000  - No issue
        //      ;password=000000000000000000 - Will be reproduced
        ////private static readonly string ConnectionString = $@"data source={DatabaseFileName};password=12345678901"; // No issue
        ////private static readonly string ConnectionString = $@"data source={DatabaseFileName};password=123456789012"; // Will be reproduced
        ////private static readonly string ConnectionString = $@"data source={DatabaseFileName};password=00000000000000000"; // No issue
        private static readonly string ConnectionString = $@"data source={DatabaseFileName};password=000000000000000000"; // Will be reproduced

        static void Main(string[] args)
        {
            // This step is preparation only. But all other steps are obligatory.
            // It creates the following tables:
            //      CREATE TABLE [MainTable] ([id] int NOT NULL)
            //      CREATE TABLE [DummyTable] ([id] int NOT NULL)
            // The second table really is not obligatory, but it shows strangeness
            // of the issue (explained later).
            CreateTables();

            // Data will be inserted in one connection and won't be read from another one.
            SQLiteConnection c0 = new SQLiteConnection(ConnectionString);
            c0.Open();
            SQLiteConnection c1 = new SQLiteConnection(ConnectionString);
            c1.Open();

            // Step 1. I guess that this query causes something to appear in the cache for connection #1.
            using (var command = c1.CreateCommand())
            {
                command.CommandText = @"SELECT id FROM [MainTable]";

                using (var dataReader = command.ExecuteReader())
                {
                }
            }

            // Step 2. Insert new row in connection #0 - it won't be read later from connection #1.
            using (var command = c0.CreateCommand())
            {
                command.CommandText = @"INSERT INTO MainTable (id) VALUES (@id)";
                command.Parameters.AddWithValue("@id", 1);

                command.ExecuteNonQuery();
            }

            // Step 3. This step is obligatory, so it forces me to think that it is bug in SQLite.
            // Query can insert new row either to MainTable or to DummyTable from connection #0.
            // In both cases, previously inserted to MainTable row won't be read from another connection #1.
            // But if row is not inserted here, then previously inserted row will be successfully read.
            using (var command = c0.CreateCommand())
            {
                command.CommandText = @"INSERT INTO DummyTable (Id) VALUES (@id)";
                ////command.CommandText = @"INSERT INTO MainTable (Id) VALUES (@id)";
                command.Parameters.AddWithValue("@id", 1);

                command.ExecuteNonQuery();
            }

            // Step 4. The issue - newly added row can't be read from another connection #1.
            using (var command = c1.CreateCommand())
            {
                command.CommandText = @"SELECT id FROM [MainTable]";
                using (var dr = command.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        Console.WriteLine("No repro. The row was read successfully.");
                    }
                    else
                    {
                        Console.WriteLine("Reproduced the issue (the row was not read).");
                    }

                }
            }

            // Just confirmation that the row can be read from connection #0 where it was inserted.
            using (var command = c0.CreateCommand())
            {
                command.CommandText = @"SELECT id FROM [MainTable]";
                using (var dr = command.ExecuteReader())
                {
                    if (dr.Read())
                    {
                        Console.WriteLine("The row was read from another connection.");
                    }
                    else
                    {
                        Console.WriteLine("Hmmm... The row was not read even from another connection (I never saw this message).");
                    }

                }
            }

            c0.Dispose();
            c1.Dispose();

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }

        private static void CreateTables()
        {
            SQLiteConnection.CreateFile(DatabaseFileName);
            using (var connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"CREATE TABLE [MainTable] ([id] int NOT NULL);
                                            CREATE TABLE [DummyTable] ([id] int NOT NULL);";
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
