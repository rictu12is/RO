using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineLeaveApplication.Controllers
{
    public class AttendanceServiceController : Controller
    {
         
        // GET: AttendanceService
        public List<string> GetLogs(string ipAddress, int port = 4370)
        {
            var helper = new SDKHelper();
            bool connected = helper.Connect("172.20.104.149", 4370); // Replace with your device IP

            if (!connected)
            {
                ViewBag.Error = "Failed to connect to device.";
                //return View(new List<TimeLog>());
            }
            var logs = helper.GetTimeLogs();

            return null;
        }
    }
}