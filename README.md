# Potato.SQLite

The `Potato.SQLite` library is a helper class designed to simplify interactions with SQLite databases in .NET applications.
It supports database initialization, data manipulation (CRUD operations), and utility functions for SQLite.

## Features

- **Database Initialization**: Automatically creates the SQLite database file if it does not exist.
- **CRUD Operations**: Provides asynchronous methods to insert, update, delete, and query data.
- **Parameterized Queries**: Protects against SQL injection.
- **Flexible Initialization**: Allows for optional configuration of base image URIs.

---

## Installation

1. Add the `Potato.SQLite` class to your project.
2. Ensure you have the `System.Data.SQLite` NuGet package installed.

```bash
dotnet add package System.Data.SQLite
```

---

## Usage

### 1. Initialize the Helper Class

```csharp
using Potato.SQLite;

// With base image URI
var helper = new Helper("C:\\databases", "example.db", new Uri("http://example.com/images"));

// Without base image URI
var helperWithoutImage = new Helper("C:\\databases", "example.db");
```

### 2. Insert Data

#### Insert a Collection of Rows

```csharp
var data = new List<Dictionary<string, object>>
{
    new Dictionary<string, object> { { "Name", "Potato" }, { "Type", "Vegetable" } },
    new Dictionary<string, object> { { "Name", "Tomato" }, { "Type", "Fruit" } },
};

await helper.InsertDataAsync("Products", data);
```

#### Insert Data and Get the Last Inserted ID

```csharp
int lastId = await helper.InsertReturnDataAsync("Products", data);
Console.WriteLine($"Last inserted ID: {lastId}");
```

---

### 3. Query Data

#### Read All Rows from a Table

```csharp
var rows = await helper.ReadAllDataAsync("Products");
foreach (var row in rows)
{
    Console.WriteLine(string.Join(", ", row.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
}
```

#### Read Data with Conditions

```csharp
var conditions = new Dictionary<string, object> { { "Type", "Vegetable" } };
var rowsWithConditions = await helper.ReadDataAsync("Products", conditions);
```

---

### 4. Update Data

#### Update Rows with Specific Conditions

```csharp
var updatedValues = new Dictionary<string, object> { { "Name", "Sweet Potato" } };
var conditions = new Dictionary<string, object> { { "Name", "Potato" } };

await helper.UpdateDataAsync("Products", updatedValues, conditions);
```

---

### 5. Delete Data

```csharp
var conditions = new Dictionary<string, object> { { "Name", "Potato" } };

await helper.DeleteDataAsync("Products", conditions);
```

---

## Notes

- **Connection Management**: The library manages database connections internally. You do not need to explicitly open or close connections.
- **Thread Safety**: Ensure proper thread synchronization if using the library in multi-threaded applications.
- **SQL Injection Prevention**: Always use parameterized queries for safe interactions.

---

## Contributing

Contributions are welcome! If you encounter a bug or want to add new features, feel free to submit a pull request or file an issue.

---

## License

This project is licensed under the MIT License.
