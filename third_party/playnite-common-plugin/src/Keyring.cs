using System.Net;
using AdysTech.CredentialManager;

namespace CommonPlugin;

public class Keyring
{
    public static void SetPassword(string servicename, string username, string password)
    {
        var credential = new NetworkCredential(username, password);
        CredentialManager.SaveCredentials(servicename + "/" + username, credential);
    }

    public static string? GetPassword(string servicename, string username)
    {
        var storedCredential = CredentialManager.GetCredentials(servicename + "/" + username);
        return storedCredential?.Password;
    }

    public static void DeletePassword(string servicename, string username)
    {
        CredentialManager.RemoveCredentials(servicename + "/" + username);
    }
}