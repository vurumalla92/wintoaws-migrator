using System;
using System.Windows.Forms;
using Microsoft.Win32;

// Sample only: this file exists so WinToAws can demonstrate detection.
// It is not built as part of the WinToAws solution on Linux.
class Program
{
    static void Main()
    {
        var form = new Form { Text = "Legacy Orders" };
        var setting = Registry.CurrentUser.OpenSubKey("Software\\LegacyOrders");
        var path = @"C:\LegacyOrders\exports\orders.csv";
        var connection = "Server=localhost;Database=Orders;Integrated Security=True;";
        Console.WriteLine(form.Text + " " + path + " " + connection + " " + setting);
    }
}
