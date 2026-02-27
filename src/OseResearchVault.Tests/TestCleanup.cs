using Microsoft.Data.Sqlite;

namespace OseResearchVault.Tests;

internal static class TestCleanup
{
    public static void DeleteDirectory(string path, int retries = 5)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        for (var attempt = 0; attempt < retries; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < retries - 1)
            {
                System.Threading.Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < retries - 1)
            {
                System.Threading.Thread.Sleep(50);
            }
        }
    }
}
