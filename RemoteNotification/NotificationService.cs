using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using log4net;
using log4net.Config;
using Newtonsoft.Json.Linq;
using PushSharp.Apple;
using PushSharp.Core;
using PushSharp.Google;
using Quartz;
using Quartz.Impl;
using RemoteNotification.Models;

namespace RemoteNotification
{
    partial class NotificationService : ServiceBase
    {
        //  Declare an instance for log4net
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
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
            XmlConfigurator.Configure();
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

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(NotificationTrigger, NotificationGroup)
                    .StartNow()
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

                    var userList = UserList();

                    if (userList.Any())
                    {
                        foreach (var user in userList)
                        {
                            if (user.DevicePlatform == "android")
                            {
                                SendNotificationForAndroid(user.DeviceToken);
                            }
                            else if (user.DevicePlatform == "ios")
                            {
                                SendNotificationForiOs(user.DeviceToken);
                            }
                        }
                    }
                    else
                    {
                        Log.Info(String.Format("List empty {0}", userList));
                    }

                    Log.Info(String.Format("Executed job at {0}", now));
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                }
            }

            private List<UserDevice> UserList()
            {
                var userDevices = new List<UserDevice>();

                var connection = Connection();

                if (connection != null)
                {
                    const string cmdShowEmployees = "SELECT * FROM AbpUserDevice;";

                    using (var sqlConnection = connection)
                    {
                        var sqlCommand = new SqlCommand { Connection = sqlConnection, CommandText = cmdShowEmployees };
                        var reader = sqlCommand.ExecuteReader();

                        Log.Info(String.Format("Executed reader {0}", reader));

                        while (reader.Read())
                        {
                            var userDevice = new UserDevice
                            {
                                Id = int.Parse(reader[0].ToString()),
                                RoleId = int.Parse(reader[1].ToString()),
                                TenantId = int.Parse(reader[2].ToString()),
                                Name = reader[3].ToString(),
                                DevicePlatform = reader[4].ToString(),
                                DeviceToken = reader[5].ToString()
                            };

                            userDevices.Add(userDevice);
                        }
                    }
                }
                else
                {
                    Log.Info(String.Format("Connection is null {0}", connection));
                }

                return userDevices;
            }

            private SqlConnection Connection()
            {
                SqlConnection conn = null;

                var cnnStringManager = ConfigurationManager.ConnectionStrings["Default"];

                try
                {
                    conn = new SqlConnection
                    {
                        ConnectionString = cnnStringManager.ConnectionString
                    };

                    conn.Open();

                    Log.Info("Connection success!!!");

                    return conn;
                }
                catch (SqlException sqlEx)
                {
                    Log.Error(String.Format("Sql exception {0}", sqlEx.Message));

                    return null;
                }
                catch (Exception ex)
                {
                    Log.Error(String.Format("Error establishing connection {0}", ex.Message));

                    if (conn != null) conn.Dispose();

                    return null;
                }
            }

            /// <summary>
            /// Notification section
            /// </summary>
            /// <param name="tokenDeviceId"></param>
            static void SendNotificationForAndroid(string tokenDeviceId)
            {
                var serverKeyFCM = "AAAA3S1Hh5s:APA91bFESXHN-xSCUs0L4HPFklfl1FR5KNGox1xVdHYNxq_2Bt5LreADm5_45aKpYf_X-8nkixiB8rx6HNmXGjk3Z3gOWxH9csEiYesHnDcfWfmEApRDITXlctypp8C7_tXFy8fy4bYL";

                // Configuration
                var config = new GcmConfiguration(serverKeyFCM);
                config.OverrideUrl("https://fcm.googleapis.com/fcm/send");

                // Create a new broker
                var gcmBroker = new GcmServiceBroker(config);

                // Wire up events
                gcmBroker.OnNotificationFailed += (notification, aggregateEx) =>
                {
                    aggregateEx.Handle(ex =>
                    {
                        // See what kind of exception it was to further diagnose
                        if (ex is GcmNotificationException)
                        {
                            var notificationException = (GcmNotificationException)ex;

                            // Deal with the failed notification
                            var gcmNotification = notificationException.Notification;
                            //var description = notificationException.Description;
                            var description = notificationException.StackTrace;

                            Log.Error(string.Format("GCM Notification Failed: ID={0}, Desc={1}", gcmNotification.MessageId, description));
                        }
                        else if (ex is GcmMulticastResultException)
                        {
                            var multicastException = (GcmMulticastResultException)ex;

                            foreach (var succeededNotification in multicastException.Succeeded)
                            {
                                Log.Info(string.Format("GCM Notification Succeeded: ID={0}", succeededNotification.MessageId));
                            }

                            foreach (var failedKvp in multicastException.Failed)
                            {
                                var n = failedKvp.Key;
                                var e = failedKvp.Value;

                                Log.Error(string.Format("GCM Notification Failed: ID={0}, Desc={1}", n.MessageId, e.Message));
                            }
                        }
                        else if (ex is DeviceSubscriptionExpiredException)
                        {
                            var expiredException = (DeviceSubscriptionExpiredException)ex;

                            var oldId = expiredException.OldSubscriptionId;
                            var newId = expiredException.NewSubscriptionId;

                            Log.Info(string.Format("Device RegistrationId Expired: {0}", oldId));

                            if (!string.IsNullOrWhiteSpace(newId))
                            {
                                // If this value isn't null, our subscription changed and we should update our database
                                Log.Info(string.Format("Device RegistrationId Changed To: {0}", newId));
                            }
                        }
                        else if (ex is RetryAfterException)
                        {
                            var retryException = (RetryAfterException)ex;
                            // If you get rate limited, you should stop sending messages until after the RetryAfterUtc date
                            Log.Info(string.Format("GCM Rate Limited, don't send more until after {0}", retryException.RetryAfterUtc));
                        }
                        else
                        {
                            Log.Error("GCM Notification Failed for some unknown reason");
                        }

                        // Mark it as handled
                        return true;
                    });
                };

                gcmBroker.OnNotificationSucceeded += (notification) =>
                {
                    Console.WriteLine(@"GCM Notification Sent!");
                    Log.Info("GCM Notification Sent!");
                };

                // Start the broker
                gcmBroker.Start();

                gcmBroker.QueueNotification(new GcmNotification
                {
                    RegistrationIds = new List<string> {
					tokenDeviceId
				},
                    Data = JObject.Parse("{ \"title\" : \"Evaluaciones\", \"message\" : \"Tienes una evaluación pendiente\", \"badge\":1 }")
                });

                // Stop the broker, wait for it to finish   
                // This isn't done after every message, but after you're
                // done with the broker
                gcmBroker.Stop();
            }

            static void SendNotificationForiOs(string tokenDeviceId)
            {
                var p12Certificate = Resources.Resources.TQPushNotifications;
                var pushCerPwd = "$TQ2017$";

                var config = new ApnsConfiguration(ApnsConfiguration.ApnsServerEnvironment.Sandbox,
                    p12Certificate, pushCerPwd);

                // Create a new broker
                var apnsBroker = new ApnsServiceBroker(config);

                // Wire up events
                apnsBroker.OnNotificationFailed += (notification, aggregateEx) =>
                {
                    aggregateEx.Handle(ex =>
                    {
                        // See what kind of exception it was to further diagnose
                        if (ex is ApnsNotificationException)
                        {
                            var notificationException = (ApnsNotificationException)ex;

                            // Deal with the failed notification
                            var apnsNotification = notificationException.Notification;
                            var statusCode = notificationException.ErrorStatusCode;

                            Log.Error(string.Format("Apple Notification Failed: ID={0}, Code={1}", apnsNotification.Identifier, statusCode));
                        }
                        else
                        {
                            // Inner exception might hold more useful information like an ApnsConnectionException
                            Log.Error(string.Format("Apple Notification Failed for some unknown reason : {0}", ex.InnerException));
                        }

                        // Mark it as handled
                        return true;
                    });
                };

                apnsBroker.OnNotificationSucceeded += (notification) =>
                {
                    Log.Info("Apple Notification Sent!");
                };

                // Start the broker
                apnsBroker.Start();

                // Queue a notification to send
                apnsBroker.QueueNotification(new ApnsNotification
                {
                    DeviceToken = tokenDeviceId,
                    Payload = JObject.Parse("{\"aps\":{\"badge\":1, \"alert\":{ \"title\": \"Evaluaciones\", \"body\": \"Tienes una evaluación pendiente\" }}}")
                });

                // Stop the broker, wait for it to finish   
                // This isn't done after every message, but after you're
                // done with the broker
                apnsBroker.Stop();
            }
        }
    }
}
