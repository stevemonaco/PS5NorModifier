namespace UartCL;
public static class Globals
{

    public static UartDatabaseService DatabaseService { get; } = new();
    public static BiosService BiosService { get; } = new();
}
