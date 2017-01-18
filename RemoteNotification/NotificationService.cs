namespace RemoteNotification
{
    using System;
    using System.ServiceProcess;

    using log4net;

    using Quartz;
    using Quartz.Impl;

    partial class NotificationService : ServiceBase
    {
        //  Declare an instance for log4net
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        //  Create constants 
        private const string NotificationJob = "NotificationRunScheduleJob";
        private const string NotificationGroup = "NotificationRunScheduleGroup";
        private const string NotificationTrigger = "NotificationRunScheduleTrigger";
        private const string NotificationScheduleStartedMsg = "Remote Notification schedule has been started";
        private const string NotificationScheduleStoppedMsg = "Remote Notification schedule has been stopped";
        private const string NotificationJobStartedMsg = "Remote Notification Job has been started";
        private const string NotificationJobStoppedMsg = "Remote Notification Job has been completed";

        public NotificationService()
        {
            this.InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            //  Auto configures log4net based on the application's configuration setting.
            log4net.Config.XmlConfigurator.Configure();
            //  Add log message when job is started
            Log.Info(NotificationScheduleStartedMsg);
            this.RunApp();
        }

        protected override void OnStop()
        {
            //  Add log messages when job is stopped
            Log.Info(NotificationScheduleStoppedMsg);
        }

        public void RunApp()
        {
            //  Construct a scheduler factory  
            ISchedulerFactory schedFact = new StdSchedulerFactory();

            //  Get a scheduler, start the schedular before triggers or anything else  
            IScheduler sched = schedFact.GetScheduler();
            sched.Start();

            //  Create job 
            var job = JobBuilder.Create<ExecutingJobs>()
                        .WithIdentity(NotificationJob, NotificationGroup)
                        .Build();
            try
            {
                //  Trigger the job to run now, and then repeat every time configured 
                var config = Common.GetConfiguration();

                //  Get configured times from Configuration.xml
                var timeToRunInDays = config.ScheduleIntervalInDays * 24 * 60;
                var timeToRunInHours = config.ScheduleIntervalInHours * 60;
                var timeToRunInMinutes = config.ScheduleIntervalInMinutes;
                var timeToRunInSeconds = config.ScheduleIntervalInSeconds;
                var scheduleTime = timeToRunInDays + timeToRunInHours + timeToRunInMinutes + timeToRunInSeconds;
                var startAtHours = config.ScheduleStartAtHours;
                var startAtMinutes = config.ScheduleStartAtMinutes;
                var startAtSeconds = config.ScheduleStartAtSeconds;

                //  Used trigger builder to create a job with scheduled times
                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(NotificationTrigger, NotificationGroup)
                    .StartAt(DateTime.Now.Date.AddHours(startAtHours)
                                              .AddMinutes(startAtMinutes)
                                              .AddSeconds(startAtSeconds))
                                              
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(scheduleTime).RepeatForever())
                    .Build();

                //  Schedule the job using the job and trigger   
                sched.ScheduleJob(job, trigger);
            }
            catch (SchedulerException se)
            {
                Log.Error(se.Message);
            }
        }

        //  Create a class to handle the executing jobs
        public class ExecutingJobs : IJob
        {
            void IJob.Execute(IJobExecutionContext context)
            {
                Log.Info(NotificationJobStartedMsg);
                this.ExecutingTasks();
                Log.InfoFormat(NotificationJobStoppedMsg);
            }

            //  Add your code here to perform your business.
            private void ExecutingTasks()
            {
                try
                {                    
                    var now = DateTime.Now.ToString("yyyyMMdd-HHMMss");

                    //  TODO JOHN: Add logic here

                    Log.Info(String.Format("Executed job at {0}", now));
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                }
            }
        }
    }
}
