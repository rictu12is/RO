using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using zkemkeeper;

namespace OnlineLeaveApplication
{
    public class SDKHelper
    {

        private CZKEM device = new CZKEM();

        public bool Connect(string ip, int port)
        {
            return device.Connect_Net(ip, port);
        }

        public List<TimeLog> GetTimeLogs()
        {
            var logs = new List<TimeLog>();
            int machineNumber = 1;

            if (!device.ReadGeneralLogData(machineNumber))
                return logs;

            string enrollNumber;
            int verifyMode, inOutMode, year, month, day, hour, minute, second, workCode = 0;

            while (device.SSR_GetGeneralLogData(machineNumber, out enrollNumber, out verifyMode, out inOutMode,
                out year, out month, out day, out hour, out minute, out second, ref workCode))
            {
                logs.Add(new TimeLog
                {
                    EnrollNumber = enrollNumber,
                    VerifyMode = verifyMode,
                    InOutMode = inOutMode,
                    Timestamp = new DateTime(year, month, day, hour, minute, second)
                });
            }

            return logs;
        }

    }


    public class TimeLog
    {
        public string EnrollNumber { get; set; }
        public int VerifyMode { get; set; }
        public int InOutMode { get; set; }
        public DateTime Timestamp { get; set; }
    }

}