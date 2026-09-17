namespace SolidworksMCP;

public static class Program
{
    public static async Task Main()
    {
        await new SolidWorksMcpServer().RunAsync().ConfigureAwait(false);
    }
}
