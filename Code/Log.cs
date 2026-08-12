using VRage.Utils;

namespace ES
{
    public static class Log
    {
        public static bool Debug;
        public static void Msg(string msg)
        {
            MyLog.Default.WriteLine($"ESTieredTech: {msg}");
        }
    }
}
