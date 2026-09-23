using ArkhivRobit.Services;

namespace ArkhivRobit.Tests;

public class DatabaseSetupTests
{
    [Fact]
    public void ToNpgsqlConnectionString_ConvertsCloudUrl()
    {
        var result = DatabaseSetup.ToNpgsqlConnectionString(
            "postgresql://user:p%40ss@ep-cool-name.eu-central-1.aws.neon.tech/arkhiv?sslmode=require");

        Assert.Contains("Host=ep-cool-name.eu-central-1.aws.neon.tech", result);
        Assert.Contains("Port=5432", result);
        Assert.Contains("Database=arkhiv", result);
        Assert.Contains("Username=user", result);
        Assert.Contains("Password=p@ss", result);
        Assert.Contains("SSL Mode=Require", result);
    }

    [Fact]
    public void ToNpgsqlConnectionString_KeepsPortAndSslMode()
    {
        var result = DatabaseSetup.ToNpgsqlConnectionString("postgres://u:p@localhost:6543/db?sslmode=disable");

        Assert.Contains("Port=6543", result);
        Assert.Contains("SSL Mode=Disable", result);
    }

    [Fact]
    public void ToNpgsqlConnectionString_LeavesKeyValueFormatUnchanged()
    {
        const string value = "Host=localhost;Database=arkhiv;Username=u;Password=p";
        Assert.Equal(value, DatabaseSetup.ToNpgsqlConnectionString(value));
    }
}
