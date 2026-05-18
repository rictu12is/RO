using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;

namespace OnlineLeaveApplication.Controllers
{
    public class NotificationController : Controller
    {
        readonly OnlineLeaveApplicationEntities db = new OnlineLeaveApplicationEntities();

        public ActionResult GetMyNotifications()
        {
            var employeeID = GetCurrentEmployeeID();
            if (!employeeID.HasValue)
            {
                return new HttpStatusCodeResult(401);
            }

            try
            {
                NotificationStore.EnsureNotificationTableExists(db);

                var unreadCount = db.Database.SqlQuery<int>(
                    "SELECT COUNT(1) FROM dbo.Notification WHERE EmployeeID = @p0 AND ReadAt IS NULL",
                    employeeID.Value).FirstOrDefault();

                var rows = db.Database.SqlQuery<NotificationRow>(
                    @"SELECT TOP 8
                        NotificationID,
                        Title,
                        Message,
                        TargetUrl,
                        ReadAt,
                        CreatedAt
                    FROM dbo.Notification
                    WHERE EmployeeID = @p0
                    ORDER BY CreatedAt DESC",
                    employeeID.Value).ToList();

                var notifications = rows.Select(row => new
                {
                    row.NotificationID,
                    row.Title,
                    row.Message,
                    row.TargetUrl,
                    isRead = row.ReadAt.HasValue,
                    createdAt = row.CreatedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)
                }).ToList();

                return Json(new
                {
                    unreadCount,
                    notifications
                }, JsonRequestBehavior.AllowGet);
            }
            catch (SqlException)
            {
                return Json(new
                {
                    unreadCount = 0,
                    notifications = new List<object>(),
                    errorMessage = "Notifications are unavailable. Please check database permissions for dbo.Notification."
                }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult MarkNotificationRead(int id)
        {
            var employeeID = GetCurrentEmployeeID();
            if (!employeeID.HasValue)
            {
                return new HttpStatusCodeResult(401);
            }

            try
            {
                NotificationStore.EnsureNotificationTableExists(db);

                db.Database.ExecuteSqlCommand(
                    @"UPDATE dbo.Notification
                    SET ReadAt = COALESCE(ReadAt, GETDATE())
                    WHERE NotificationID = @p0 AND EmployeeID = @p1",
                    id,
                    employeeID.Value);

                return Json(new
                {
                    unreadCount = GetUnreadCount(employeeID.Value)
                });
            }
            catch (SqlException)
            {
                return Json(new
                {
                    unreadCount = 0,
                    errorMessage = "Notifications are unavailable."
                });
            }
        }

        [HttpPost]
        public ActionResult MarkAllNotificationsRead()
        {
            var employeeID = GetCurrentEmployeeID();
            if (!employeeID.HasValue)
            {
                return new HttpStatusCodeResult(401);
            }

            try
            {
                NotificationStore.EnsureNotificationTableExists(db);

                db.Database.ExecuteSqlCommand(
                    @"UPDATE dbo.Notification
                    SET ReadAt = COALESCE(ReadAt, GETDATE())
                    WHERE EmployeeID = @p0 AND ReadAt IS NULL",
                    employeeID.Value);

                return Json(new
                {
                    unreadCount = 0
                });
            }
            catch (SqlException)
            {
                return Json(new
                {
                    unreadCount = 0,
                    errorMessage = "Notifications are unavailable."
                });
            }
        }

        int GetUnreadCount(short employeeID)
        {
            return db.Database.SqlQuery<int>(
                "SELECT COUNT(1) FROM dbo.Notification WHERE EmployeeID = @p0 AND ReadAt IS NULL",
                employeeID).FirstOrDefault();
        }

        short? GetCurrentEmployeeID()
        {
            if (Session["EmployeeID"] == null)
            {
                return null;
            }

            return Convert.ToInt16(Session["EmployeeID"]);
        }

        class NotificationRow
        {
            public int NotificationID { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
            public string TargetUrl { get; set; }
            public DateTime? ReadAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
