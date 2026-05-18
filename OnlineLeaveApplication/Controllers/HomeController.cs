using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using zkemkeeper;

namespace OnlineLeaveApplication.Controllers
{
    public class HomeController : Controller
    {
        OnlineLeaveApplicationEntities db = new OnlineLeaveApplicationEntities();
        public ActionResult Login(Employee employee) {
            //CZKEM cz = new CZKEM();

            if (employee.Username == null)
            {
                ViewBag.Message = "";
                return View();
            }
            else {
                var obj = db.Employees.Where(a => a.Username == employee.Username && a.Password == employee.Password).FirstOrDefault();
                if (obj == null)
                {
                    ViewBag.Message = "Incorrect username/password";

                    return View();
                }
                else
                {
                    int employeeID = obj.EmployeeID;

                    Session["EmployeeID"] = employeeID;
                    Session["EmployeeName"] = string.Join(" ", new[] { obj.FirstName, obj.LastName }
                        .Where(name => !string.IsNullOrWhiteSpace(name)));
                    Session["RoleID"] = obj.PositionID;
                    Session["RoleName"] = obj.Position == null ? string.Empty : obj.Position.PositionName;
                    //var result = db.MainOffices
                    //    .Where(m => m.MainOfficeID == obj.Office.MainOfficeID)
                    //    .Select(m => new
                    //    {
                    //        ForInitialReview = obj != null && m.ForInitialReview == obj.EmployeeID,
                    //        ForReview = obj != null && m.ForReview == obj.EmployeeID,
                    //        ForApproval = obj != null && m.ForApproval == obj.EmployeeID
                    //    }).FirstOrDefault();
                    var result = db.MainOffices
                                .Where(m => m.MainOfficeID == obj.Office.MainOfficeID)
                                .Select(m => new
                                {
                                    ForInitialReview = m.ForInitialReview == employeeID,
                                    ForReview = m.ForReview == employeeID,
                                    ForApproval = m.ForApproval == employeeID
                                })
                                .FirstOrDefault();
                    Session["Status"] = result.ForInitialReview ? 2 : result.ForReview ? 3 : result.ForApproval ? 4 : 0;

                    return RedirectToAction("RegionalOrders", "Home");
                }
            }
             
        }

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Login", "Home");
        }

        public ActionResult Index()
        {
            var i = Environment.Is64BitOperatingSystem;
            if (Convert.ToInt16(Session["EmployeeID"]) == 0)
            {
                return RedirectToAction("Login", "Home");
            }

            // Fetch categories from the database
            var typeOfLeaves = db.TypeOfLeaves
                                     .Select(c => new SelectListItem
                                     {
                                         Value = c.TypeOfLeaveID.ToString(),
                                         Text = c.TypeOfLeave1.ToString()
                                     })
                                     .ToList();

            // Pass the categories to the view using ViewBag or ViewModel
            ViewBag.TypeOfLeaves = typeOfLeaves; 
           var serverDate = db.Database.SqlQuery<DateTime>("SELECT GETDATE()"); 
            ViewBag.disabledDates = db.LeaveApplicationDetailInclusiveDates.Where(a => a.LeaveApplicationDetail.LeaveApplication.EmployeeID == 1).Select(a => a.LeaveDate.ToString()).ToArray();
            return View();
        }
        public ActionResult RegionalOrders()
        {
            if (Session["EmployeeID"] == null)
            {
                return RedirectToAction("Login", "Home");
            }

            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult RegionalOrderCalendar()
        {
            if (Session["EmployeeID"] == null)
            {
                return RedirectToAction("Login", "Home");
            }

            return View();
        }

        public ActionResult Signatories()
        {
            var signatories = db.Employees
                                     .Select(c => new SelectListItem
                                     {
                                         Value = c.EmployeeID.ToString(),
                                         Text = c.LastName + ", " + c.FirstName
                                     }).ToList();
            ViewBag.Signatories = signatories;
            ViewBag.Message = "Your application description page.";

            return View();
        }
        public ActionResult ManageRegionalOrder()
        {
            if (!CurrentUserCanManageRegionalOrders())
            {
                return RedirectToAction("RegionalOrders", "Home");
            }

            ViewBag.Message = "Your application description page.";
            // Fetch categories from the database
            var employees = db.Employees
                                     .Select(c => new SelectListItem
                                     {
                                         Value = c.EmployeeID.ToString(),
                                         Text = c.LastName + ", " + c.FirstName + " " + c.MiddleName
                                     })
                                     .ToList();

            // Pass the categories to the view using ViewBag or ViewModel
            ViewBag.Employees = employees;
            return View();
        }

        public ActionResult ListofApplications()
        {
            var employeeID = Convert.ToInt16(Session["EmployeeID"]);
            var obj = db.Employees.Where(a => a.EmployeeID == employeeID).FirstOrDefault();

            //var result = db.MainOffices
            //            .Where(m => m.MainOfficeID == obj.Office.MainOfficeID)
            //            .Select(m => new
            //            {
            //                ForInitialReview = m.ForInitialReview == employeeID,
            //                ForReview = m.ForReview == employeeID,
            //                ForApproval = m.ForApproval == employeeID
            //            }).FirstOrDefault();

            //Session["Status"] = result.ForInitialReview ? 2 : result.ForReview ? 3 : 4;
            return View();
        }

        public ActionResult CTOApplication()
        {
            ViewBag.Message = "Your application description page.";
            ViewBag.CTO = db.RegionalOrders.Count(a => a.isCompensatoryOvertimeCredit == true);
            return View();
        }

        public ActionResult Dashboard()
        {
            ViewBag.ForReview = 10;
            ViewBag.ForCertification = 30;
            ViewBag.Approved = 20;
            ViewBag.Disapproved = 330;
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        bool CurrentUserCanManageRegionalOrders()
        {
            return Session["RoleID"] != null && Convert.ToInt32(Session["RoleID"]) == 4;
        }
    }
}
