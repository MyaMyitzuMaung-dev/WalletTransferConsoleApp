//Dapper
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace WalletTransferConsoleApp;

public class WalletSample
{
    private readonly SqlConnectionStringBuilder sb = new()
    {
        DataSource = ".",
        InitialCatalog = "IPB2WalletTransfer",
        UserID = "sa",
        Password = "12345",
        TrustServerCertificate = true
    };

    public void RunWalletCRUD()
    {
        Read();
        Create();
        Update();
        Delete();
    }

    // READ (All wallets except soft-deleted)
    private void Read()
    {
        using IDbConnection db = new SqlConnection(sb.ConnectionString);
        db.Open();

        string query = "SELECT * FROM Wallets WHERE IsDelete = 0";
        var wallets = db.Query<WalletDto>(query).ToList();

        foreach (var w in wallets)
        {
            Console.WriteLine($"WalletId: {w.WalletId}, Name: {w.FullName}, Mobile: {w.MobileNo}, Balance: {w.Balance}");
        }
    }

    // CREATE Wallet
    private void Create()
    {
        using IDbConnection db = new SqlConnection(sb.ConnectionString);
        db.Open();

        Console.Write("Enter Full Name: ");
        string name = Console.ReadLine()!;

        Console.Write("Enter Mobile No: ");
        string mobile = Console.ReadLine()!;

        // 1️ Get the current max WalletId (like W001, W002, ...)
        string getMaxIdQuery = "SELECT MAX(WalletId) FROM Wallets";
        string? maxId = db.ExecuteScalar<string>(getMaxIdQuery);

        int newNumber = 1; // default if no wallets exist
        if (!string.IsNullOrEmpty(maxId) && maxId.Length > 1)
        {
            // Extract number part and increment
            string numberPart = maxId[1..]; // remove leading W
            if (int.TryParse(numberPart, out int num))
            {
                newNumber = num + 1;
            }
        }

        string walletId = $"W{newNumber:000}"; // e.g., W004

        string query = @"INSERT INTO Wallets (WalletId, FullName, MobileNo, Balance, CreatedAt, IsDelete)
                     VALUES (@WalletId, @FullName, @MobileNo, @Balance, @CreatedAt, 0)";

        int result = db.Execute(query, new
        {
            WalletId = walletId,
            FullName = name,
            MobileNo = mobile,
            Balance = 0m,
            CreatedAt = DateTime.Now
        });

        Console.WriteLine(result > 0
            ? $"Wallet created successfully! WalletId = {walletId}"
            : "Failed to create wallet");
    }


    // UPDATE Wallet (e.g., name)
    private void Update()
    {
        using IDbConnection db = new SqlConnection(sb.ConnectionString);
        db.Open();

        Console.Write("Enter WalletId to update: ");
        string id = Console.ReadLine()!;

        Console.Write("Enter new Full Name: ");
        string name = Console.ReadLine()!;

        string query = @"UPDATE Wallets 
                         SET FullName = @FullName
                         WHERE WalletId = @WalletId AND IsDelete = 0";

        int result = db.Execute(query, new { FullName = name, WalletId = id });

        Console.WriteLine(result > 0 ? "Wallet updated successfully" : "Failed to update wallet");
    }

    // DELETE (Soft Delete)
    private void Delete()
    {
        using IDbConnection db = new SqlConnection(sb.ConnectionString);
        db.Open();

        Console.Write("Enter WalletId to delete: ");
        string id = Console.ReadLine()!;

        string query = @"UPDATE Wallets
                         SET IsDelete = 1
                         WHERE WalletId = @WalletId";

        int result = db.Execute(query, new { WalletId = id });

        Console.WriteLine(result > 0 ? "Wallet deleted (soft) successfully" : "Failed to delete wallet");
    }
}

// DTO for mapping query results
public class WalletDto
{
    public string WalletId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDelete { get; set; }
}
