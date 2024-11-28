/*
 * MIT License
 * 
 * Copyright (c) 2024 https://github.com/potatoscript
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Potato.SQLite
{
    /// <summary>
    /// Helper class for interacting with the SQLite database.
    /// Manages database connection, initialization, and data manipulation (insert, update, delete).
    /// </summary>
    public class Helper

    {
        private readonly string _connectionString; // Connection string used to establish a connection to the SQLite database.

        /// <summary>
        /// Path to the database file.
        /// </summary>
        public string DbFilePath { get; }

        /// <summary>
        /// URI for base image path used in the application.
        /// </summary>
        public Uri BaseImageUri { get; }

        private Dictionary<string, string> resources; // Dictionary to hold resources, such as base image URI.

        /// <summary>
        /// SQLite connection to interact with the database.
        /// </summary>
        public SQLiteConnection Connection { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Helper"/> class.
        /// Sets up the database connection string, file paths, and localized resources.
        /// If the database file doesn't exist, it will be created automatically.
        /// </summary>
        public Helper(string dbDirectory, string dbName, Uri BaseImageUri)
        {
            // Ensure that the directory and name are valid.
            if (string.IsNullOrEmpty(dbDirectory) || string.IsNullOrEmpty(dbName))
                throw new InvalidOperationException("Database directory or name is not defined in resources.");

            // Combine the directory path with the database file name to get the full file path.
            DbFilePath = Path.Combine(dbDirectory, dbName);

            // Get the base image URI from resources (assumed to be used later in the app).
            this.BaseImageUri = BaseImageUri;

            // Set the connection string for SQLite using the database file path.
            _connectionString = $"Data Source={DbFilePath};Version=3;";

            // Call InitializeDatabase method to set up the database if it's not present.
            InitializeDatabase();
        }

        public Helper(string dbDirectory, string dbName)
        {
            // Ensure that the directory and name are valid.
            if (string.IsNullOrEmpty(dbDirectory) || string.IsNullOrEmpty(dbName))
                throw new InvalidOperationException("Database directory or name is not defined in resources.");

            // Combine the directory path with the database file name to get the full file path.
            DbFilePath = Path.Combine(dbDirectory, dbName);

            // Set the connection string for SQLite using the database file path.
            _connectionString = $"Data Source={DbFilePath};Version=3;";

            // Call InitializeDatabase method to set up the database if it's not present.
            InitializeDatabase();
        }

        /// <summary>
        /// Initializes the SQLite database by checking if the database file exists.
        /// If the file doesn't exist, it creates a new SQLite database file.
        /// </summary>
        private void InitializeDatabase()
        {
            // Check if the SQLite database file exists.
            if (!File.Exists(DbFilePath))
            {
                // If the database file doesn't exist, create a new one.
                SQLiteConnection.CreateFile(DbFilePath);
            }
        }

        /// <summary>
        /// Executes a non-query SQL command asynchronously (e.g., INSERT, UPDATE, DELETE).
        /// This method is used for database operations that do not return data (just execute commands).
        /// </summary>
        /// <param name="query">SQL query string to execute.</param>
        public async Task ExecuteNonQueryAsync(string query)
        {
            // Ensure that the query string is not null or empty before executing.
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            // Create a new SQLite connection using the connection string.
            using var connection = new SQLiteConnection(_connectionString);

            // Open the database connection asynchronously.
            await connection.OpenAsync();

            // Create a command object to execute the query.
            using var command = new SQLiteCommand(query, connection);

            // Execute the non-query SQL command asynchronously (INSERT, UPDATE, DELETE).
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Checks if a table is empty by counting the number of rows in the table.
        /// Returns true if the table is empty (has zero rows).
        /// </summary>
        /// <param name="tableName">Name of the table to check.</param>
        public async Task<bool> IsTableEmptyAsync(string tableName)
        {
            // Ensure that the table name is not null or empty.
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));

            // Create a new SQLite connection.
            using var connection = new SQLiteConnection(_connectionString);

            // Open the connection asynchronously.
            await connection.OpenAsync();

            // Query to count the rows in the specified table.
            string query = $"SELECT COUNT(*) FROM {tableName};";

            // Create a command object with the query.
            using var command = new SQLiteCommand(query, connection);

            // Execute the query to get the row count.
            var count = (long)await command.ExecuteScalarAsync();

            // Return true if the table is empty (count == 0), otherwise false.
            return count == 0;
        }

        /// <summary>
        /// Inserts initial data into a table if the table is empty.
        /// This ensures that the table gets populated with necessary data on first use.
        /// </summary>
        /// <param name="tableName">Name of the table where data should be inserted.</param>
        /// <param name="data">A collection of dictionaries containing the data to insert.</param>
        public async Task InsertInitialDataIfEmptyAsync(string tableName, IEnumerable<IDictionary<string, object>> data)
        {
            // Check if the table is empty.
            if (await IsTableEmptyAsync(tableName))
            {
                // If the table is empty, insert the initial data.
                await InsertDataAsync(tableName, data);
            }
        }

        /// <summary>
        /// Inserts data into a specified table asynchronously.
        /// This method constructs an SQL INSERT command for each item in the provided data collection.
        /// </summary>
        /// <param name="tableName">Name of the table to insert data into.</param>
        /// <param name="data">A collection of dictionaries, each representing a row of data to insert into the table.</param>
        public async Task InsertDataAsync(string tableName, IEnumerable<IDictionary<string, object>> data)
        {
            // If there is no data, exit early.
            if (!data.Any()) return;

            // Create a new SQLite connection.
            using var connection = new SQLiteConnection(_connectionString);

            // Open the connection asynchronously.
            await connection.OpenAsync();

            // Extract the column names (keys) from the first dictionary in the data collection.
            var keys = string.Join(", ", data.First().Keys);

            // Prepare the parameterized SQL query string for inserting data.
            var parameters = string.Join(", ", data.First().Keys.Select(k => $"@{k}"));

            // Construct the INSERT SQL query.
            string insertQuery = $"INSERT INTO {tableName} ({keys}) VALUES ({parameters});";

            // Create a command object with the insert query.
            using var insertCommand = new SQLiteCommand(insertQuery, connection);

            // Loop through each item in the data collection.
            foreach (var item in data)
            {
                // Clear previous parameters to avoid conflicts.
                insertCommand.Parameters.Clear();

                // Add parameters for each column in the current item (row).
                foreach (var kvp in item)
                {
                    insertCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                }

                // Execute the insert command for the current row.
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> InsertReturnDataAsync(string tableName, IEnumerable<IDictionary<string, object>> data)
        {
            // If there is no data, exit early.
            if (!data.Any()) return -1; // Return -1 or some other default value to indicate no insert.

            // Create a new SQLite connection.
            using var connection = new SQLiteConnection(_connectionString);

            // Open the connection asynchronously.
            await connection.OpenAsync();

            // Extract the column names (keys) from the first dictionary in the data collection.
            var keys = string.Join(", ", data.First().Keys);

            // Prepare the parameterized SQL query string for inserting data.
            var parameters = string.Join(", ", data.First().Keys.Select(k => $"@{k}"));

            // Construct the INSERT SQL query.
            string insertQuery = $"INSERT INTO {tableName} ({keys}) VALUES ({parameters});";

            // Create a command object with the insert query.
            using var insertCommand = new SQLiteCommand(insertQuery, connection);

            // Loop through each item in the data collection.
            foreach (var item in data)
            {
                // Clear previous parameters to avoid conflicts.
                insertCommand.Parameters.Clear();

                // Add parameters for each column in the current item (row).
                foreach (var kvp in item)
                {
                    insertCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                }

                // Execute the insert command for the current row.
                await insertCommand.ExecuteNonQueryAsync();
            }

            // After the insert, get the last inserted row ID.
            var getLastInsertedIdQuery = "SELECT LAST_INSERT_ROWID();";
            using var lastInsertedIdCommand = new SQLiteCommand(getLastInsertedIdQuery, connection);
            var lastInsertedId = await lastInsertedIdCommand.ExecuteScalarAsync();

            // Convert the result to an integer and return it.
            return Convert.ToInt32(lastInsertedId);
        }



        /// <summary>
        /// Reads all data from a specified table asynchronously.
        /// </summary>
        public async Task<IEnumerable<Dictionary<string, object>>> ReadAllDataAsync(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));

            var result = new List<Dictionary<string, object>>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string query = $"SELECT * FROM {tableName} ORDER BY Id;";
            using var command = new SQLiteCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }

                result.Add(row);
            }

            return result;
        }

        /// <summary>
        /// Reads data from a table with conditions asynchronously.
        /// </summary>
        public async Task<IEnumerable<Dictionary<string, object>>> ReadDataAsync(string tableName, Dictionary<string, object> conditions)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
            if (conditions == null || !conditions.Any()) throw new ArgumentException("Conditions cannot be null or empty.", nameof(conditions));

            var result = new List<Dictionary<string, object>>();

            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            string query = BuildSelectQuery(tableName, conditions);
            using var command = new SQLiteCommand(query, connection);

            foreach (var condition in conditions)
            {
                command.Parameters.AddWithValue($"@{condition.Key}", condition.Value);
            }

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }

                result.Add(row);
            }

            return result;
        }

        /// <summary>
        /// Builds the SQL SELECT query dynamically based on the provided table name and filtering conditions.
        /// This ensures efficient query building with parameterized inputs to prevent SQL injection.
        /// </summary>
        /// <param name="tableName">The name of the table to query from.</param>
        /// <param name="conditions">A dictionary of conditions to be included in the WHERE clause.</param>
        /// <returns>A string representing the complete SELECT query.</returns>
        private string BuildSelectQuery(string tableName, Dictionary<string, object> conditions)
        {
            var queryBuilder = new StringBuilder($"SELECT * FROM {tableName} WHERE 1=1");

            foreach (var condition in conditions)
            {
                queryBuilder.Append($" AND {condition.Key} = @{condition.Key}");
            }

            return queryBuilder.ToString(); // Return the query string
        }

        /// <summary>
        /// Executes a parameterized SELECT query and returns the result as a collection of dictionaries.
        /// </summary>
        /// <param name="connection">The open SQLite connection used to execute the query.</param>
        /// <param name="selectQuery">The SQL SELECT query to be executed.</param>
        /// <param name="conditions">A dictionary of conditions used to filter the rows returned by the query.</param>
        /// <returns>An enumerable collection of dictionaries representing the filtered rows from the database.</returns>
        private IEnumerable<Dictionary<string, object>> ExecuteSelectQuery(SQLiteConnection connection, string selectQuery, Dictionary<string, object> conditions)
        {
            var result = new List<Dictionary<string, object>>();

            using (var command = new SQLiteCommand(selectQuery, connection))
            {
                foreach (var condition in conditions)
                {
                    command.Parameters.AddWithValue($"@{condition.Key}", condition.Value);
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.GetValue(i);
                        }

                        result.Add(row); // Add the row to the result list
                    }
                }
            }

            return result; // Return the list of rows as result
        }

        /// <summary>
        /// Asynchronously updates the specified table by setting new values for columns that match the given conditions.
        /// It constructs an SQL UPDATE query dynamically and executes it asynchronously using the provided updated values and conditions.
        /// </summary>
        /// <param name="tableName">The name of the table to update data in.</param>
        /// <param name="updatedValues">A dictionary containing the columns and their new values to be updated.</param>
        /// <param name="conditions">A dictionary containing the conditions to match the rows that need to be updated.</param>
        public async Task UpdateDataAsync(string tableName, Dictionary<string, object> updatedValues, Dictionary<string, object> conditions)
        {
            ValidateParameters(tableName, updatedValues, conditions);

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Build SQL query and parameters asynchronously
                    var updateQuery = BuildUpdateQuery(tableName, updatedValues, conditions);
                    await ExecuteUpdateQueryAsync(connection, updateQuery, updatedValues, conditions);
                }
            }
            catch (SQLiteException ex)
            {
                // Log the exception (logging mechanism should be implemented)
                throw new InvalidOperationException("An error occurred while updating data in the database.", ex);
            }
        }
        /// <summary>
        /// Validates the input parameters to ensure they are not null or empty.
        /// </summary>
        private void ValidateParameters(string tableName, Dictionary<string, object> updatedValues, Dictionary<string, object> conditions)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
            }

            if (updatedValues == null || updatedValues.Count == 0)
            {
                throw new ArgumentException("Updated values cannot be null or empty.", nameof(updatedValues));
            }

            if (conditions == null || conditions.Count == 0)
            {
                throw new ArgumentException("Conditions cannot be null or empty.", nameof(conditions));
            }
        }

        /// <summary>
        /// Executes the UPDATE query asynchronously using the provided connection, updated values, and conditions.
        /// </summary>
        private async Task ExecuteUpdateQueryAsync(SQLiteConnection connection, string updateQuery, Dictionary<string, object> updatedValues, Dictionary<string, object> conditions)
        {
            using (var updateCommand = new SQLiteCommand(updateQuery, connection))
            {
                // Add the parameters for the updated values to the command
                foreach (var kvp in updatedValues)
                {
                    updateCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                }

                // Add the parameters for the conditions to the command
                foreach (var kvp in conditions)
                {
                    updateCommand.Parameters.AddWithValue($"@condition_{kvp.Key}", kvp.Value);
                }

                // Execute the update command asynchronously
                await updateCommand.ExecuteNonQueryAsync();
            }
        }
        /// <summary>
        /// Asynchronously updates all rows in the specified table by setting new values for the specified columns.
        /// This method does not use any conditions, meaning it updates every row in the table.
        /// </summary>
        /// <param name="tableName">The name of the table to update data in.</param>
        /// <param name="updatedValues">A dictionary containing the columns and their new values to be updated for every row.</param>
        public async Task UpdateAllDataAsync(string tableName, Dictionary<string, object> updatedValues)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
            }

            if (updatedValues == null || updatedValues.Count == 0)
            {
                throw new ArgumentException("Updated values cannot be null or empty.", nameof(updatedValues));
            }

            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Build SQL query and parameters asynchronously
                    var updateQuery = BuildUpdateQuery(tableName, updatedValues);
                    await ExecuteUpdateQueryAsync(connection, updateQuery, updatedValues);
                }
            }
            catch (SQLiteException ex)
            {
                // Log the exception (logging mechanism should be implemented)
                throw new InvalidOperationException("An error occurred while updating data in the database.", ex);
            }
        }

        /// <summary>
        /// Builds the SQL UPDATE query based on the given table name, updated values, and conditions.
        /// </summary>
        private string BuildUpdateQuery(string tableName, Dictionary<string, object> updatedValues, Dictionary<string, object> conditions)
        {
            var setClause = string.Join(", ", updatedValues.Keys.Select(k => $"{k} = @{k}"));
            var whereClause = string.Join(" AND ", conditions.Keys.Select(k => $"{k} = @condition_{k}"));
            return $"UPDATE {tableName} SET {setClause} WHERE {whereClause};";
        }

        /// <summary>
        /// Builds the SQL UPDATE query based on the given table name and updated values.
        /// </summary>
        private string BuildUpdateQuery(string tableName, Dictionary<string, object> updatedValues)
        {
            var setClause = string.Join(", ", updatedValues.Keys.Select(k => $"{k} = @{k}"));
            return $"UPDATE {tableName} SET {setClause};";
        }
        /// <summary>
        /// Executes the UPDATE query asynchronously using the provided connection and updated values.
        /// </summary>
        /// <param name="connection">The open SQLite connection.</param>
        /// <param name="updateQuery">The SQL UPDATE query to execute.</param>
        /// <param name="updatedValues">The updated values to be used as parameters in the SQL query.</param>
        private async Task ExecuteUpdateQueryAsync(SQLiteConnection connection, string updateQuery, Dictionary<string, object> updatedValues)
        {
            using (var updateCommand = new SQLiteCommand(updateQuery, connection))
            {
                // Add the parameters for the updated values to the command
                foreach (var kvp in updatedValues)
                {
                    updateCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                }

                // Execute the update command asynchronously
                await updateCommand.ExecuteNonQueryAsync();
            }
        }
        /// <summary>
        /// Deletes rows from the specified table based on the provided conditions.
        /// This method dynamically constructs a SQL DELETE query and executes it.
        /// </summary>
        /// <param name="tableName">The name of the table to delete data from.</param>
        /// <param name="conditions">A dictionary containing column names as keys and their corresponding values as conditions to filter the rows to be deleted.</param>
        public async Task DeleteDataAsync(string tableName, Dictionary<string, object> conditions)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
            }

            if (conditions == null || conditions.Count == 0)
            {
                throw new ArgumentException("Conditions dictionary cannot be null or empty.", nameof(conditions));
            }

            try
            {
                // Open connection to SQLite database
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    await connection.OpenAsync(); // Open connection asynchronously

                    // Build SQL query and parameters
                    var deleteQuery = BuildDeleteQuery(tableName, conditions);
                    await ExecuteDeleteQueryAsync(connection, deleteQuery, conditions); // Execute delete query asynchronously
                }
            }
            catch (SQLiteException ex)
            {
                // Log the exception (logging mechanism should be implemented in the application)
                throw new InvalidOperationException("An error occurred while deleting data from the database.", ex);
            }
        }


        /// <summary>
        /// Builds the SQL DELETE query based on the given table name and conditions.
        /// </summary>
        /// <param name="tableName">The name of the table to delete from.</param>
        /// <param name="conditions">A dictionary containing the conditions to filter the rows to be deleted.</param>
        /// <returns>The SQL DELETE query string.</returns>
        private string BuildDeleteQuery(string tableName, Dictionary<string, object> conditions)
        {
            var whereClause = string.Join(" AND ", conditions.Keys.Select(k => $"{k} = @{k}"));
            return $"DELETE FROM {tableName} WHERE {whereClause};";
        }

        /// <summary>
        /// Executes the DELETE query asynchronously using the provided connection and conditions.
        /// </summary>
        /// <param name="connection">The open SQLite connection.</param>
        /// <param name="deleteQuery">The SQL DELETE query to execute.</param>
        /// <param name="conditions">The conditions to be used as parameters in the SQL query.</param>
        private async Task ExecuteDeleteQueryAsync(SQLiteConnection connection, string deleteQuery, Dictionary<string, object> conditions)
        {
            using (var deleteCommand = new SQLiteCommand(deleteQuery, connection))
            {
                // Add the parameters for the conditions to the command
                foreach (var kvp in conditions)
                {
                    deleteCommand.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value);
                }

                // Execute the delete command asynchronously
                await deleteCommand.ExecuteNonQueryAsync();
            }
        }


    }
}