using System.ServiceProcess;

namespace RemoteNotification
{
    class Program
    {
        static void Main()
        {
            RunAsService();
        }

        static void RunAsService()
        {
            ServiceBase[] servicesToRun;
            servicesToRun = new ServiceBase[] { new NotificationService() };
            ServiceBase.Run(servicesToRun);
        }
    }
}
