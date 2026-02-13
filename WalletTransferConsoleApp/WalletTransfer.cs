using Microsoft.Data.SqlClient;
using System.Transactions;

namespace WalletTransferConsoleApp;

public class WalletTransfer
{
    private readonly SqlConnectionStringBuilder sb = new()
    {
        DataSource = ".",
        InitialCatalog = "IPB2WalletTransfer",
        UserID = "sa",
        Password = "12345",
        TrustServerCertificate = true
    };

    public void Run()
    {
        Console.WriteLine("=== Mobile Wallet Transfer ===");

        Console.Write("Enter Your Mobile No: ");
        string senderMobile = Console.ReadLine()!;

        Console.Write("Enter Recipient Mobile No: ");
        string receiverMobile = Console.ReadLine()!;

        Console.Write("Enter Amount: ");
        if (!decimal.TryParse(Console.ReadLine(), out decimal amount))
        {
            Console.WriteLine("Invalid amount.");
            return;
        }

        ExecuteTransfer(senderMobile, receiverMobile, amount);
    }

    private void ExecuteTransfer(string fromMobile, string toMobile, decimal amount)
    {
        using SqlConnection connection = new(sb.ConnectionString);
        connection.Open();

        using SqlTransaction transaction = connection.BeginTransaction();
        //sqlTransaction = atomic operation လုပ်နိုင်အောင် (sender deduct + receiver add + transaction record) မှားရင် rollback လုပ်နိုင်
        try
        {
            //sender & receiver info in one query each
            var sender = GetWallet(connection, transaction, fromMobile);
            var receiver = GetWallet(connection, transaction, toMobile);

            if (receiver == null)
            {
                Console.WriteLine("Recipient not found.");
                transaction.Rollback();//this means cancel the transaction, no changes will be made to the database
                return;
            }

            if (fromMobile == toMobile)
            {
                Console.WriteLine("Cannot transfer to yourself.");
                transaction.Rollback();
                return;
            }

            if (sender == null || sender.Balance < amount)
            {
                Console.WriteLine("Insufficient funds.");
                transaction.Rollback();
                return;
            }

            // Move money
            UpdateBalance(connection, transaction, fromMobile, -amount);
            UpdateBalance(connection, transaction, toMobile, amount);

            // Create transaction record (double-entry)
            string txnId = Guid.NewGuid().ToString().ToUpper()[..8];
            DateTime now = DateTime.Now;

            InsertTransaction(connection, transaction, txnId, fromMobile, toMobile, amount, "Send", now);
            InsertTransaction(connection, transaction, txnId, fromMobile, toMobile, amount, "Receive", now);

            transaction.Commit();

            Console.WriteLine($"\nSuccess! Sent ${amount} to {receiver.FullName}.");
            Console.WriteLine($"New Balance: ${sender.Balance - amount}");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine("Transfer Failed: " + ex.Message);
        }
    }

    private Wallet? GetWallet(SqlConnection conn, SqlTransaction tx, string mobile)
    {
        string query = "SELECT WalletId, FullName, Balance FROM Wallets WHERE MobileNo = @Mobile AND IsDelete = 0";
        SqlCommand cmd = new(query, conn, tx);
        cmd.Parameters.AddWithValue("@Mobile", mobile); //Parameter ကို Value ထည့်

        using SqlDataReader reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var wallet = new Wallet(
            reader.GetString(0), //WalletId
            reader.GetString(1), //FullName
            mobile,              //MobileNo (from input)
            reader.GetDecimal(2) //Balance
        );
        reader.Close();
        return wallet;
    }

    private void UpdateBalance(SqlConnection conn, SqlTransaction tx, string mobile, decimal amount)
    {
        string query = "UPDATE Wallets SET Balance = Balance + @Amount WHERE MobileNo = @Mobile";
        SqlCommand cmd = new(query, conn, tx);
        cmd.Parameters.AddWithValue("@Amount", amount);
        cmd.Parameters.AddWithValue("@Mobile", mobile);
        cmd.ExecuteNonQuery();
    }

    //Transaction Record ထည့်မယ့် Method
    private void InsertTransaction(SqlConnection conn, SqlTransaction tx, string txnId,
        string from, string to, decimal amount, string message, DateTime time)
    {
        string query = @"INSERT INTO Transactions
                         (TxnId, FromMobileNo, ToMobileNo, Amount, Message, Timestamp)
                         VALUES (@TxnId, @From, @To, @Amount, @Message, @Time)";
        SqlCommand cmd = new(query, conn, tx);
        cmd.Parameters.AddWithValue("@TxnId", txnId);
        cmd.Parameters.AddWithValue("@From", from);
        cmd.Parameters.AddWithValue("@To", to);
        cmd.Parameters.AddWithValue("@Amount", amount);
        cmd.Parameters.AddWithValue("@Message", message);
        cmd.Parameters.AddWithValue("@Time", time);
        cmd.ExecuteNonQuery();
    }
}
