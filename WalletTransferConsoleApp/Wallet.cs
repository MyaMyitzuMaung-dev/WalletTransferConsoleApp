namespace WalletTransferConsoleApp;

public class Wallet(string walletId, string fullName, string mobileNo, decimal balance)
{
    public string WalletId { get; set; } = walletId;
    public string FullName { get; set; } = fullName;
    public string MobileNo { get; set; } = mobileNo;
    public decimal Balance { get; set; } = balance;
}
