using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineLeaveApplication.Controllers
{
    
    public class RegionalOrderController : Controller
    {
        OnlineLeaveApplicationEntities db = new OnlineLeaveApplicationEntities();
        const int MaxRegionalOrderFileBytes = 10 * 1024 * 1024;
        // GET: RegionalOrder

        public ActionResult SaveRegionalOrder(HttpPostedFileBase file, string jsonData)
        {
            if (!CurrentUserCanManageRegionalOrders())
            {
                return new HttpStatusCodeResult(403);
            }

            if (file == null || file.ContentLength == 0)
            {
                Response.StatusCode = 400;
                return Json("Please attach a PDF file.", JsonRequestBehavior.AllowGet);
            }

            if (file.ContentLength > MaxRegionalOrderFileBytes)
            {
                Response.StatusCode = 400;
                return Json("The attached file must not exceed 10 MB.", JsonRequestBehavior.AllowGet);
            }

            if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                Response.StatusCode = 400;
                return Json("Only PDF files are allowed.", JsonRequestBehavior.AllowGet);
            }

            if (Path.GetFileName(file.FileName).Length > 255)
            {
                Response.StatusCode = 400;
                return Json("The attached file name must not exceed 255 characters.", JsonRequestBehavior.AllowGet);
            }

            var regionalOrder = JsonConvert.DeserializeObject<RegionalOrder>(jsonData);
            var validationMessage = ValidateRegionalOrder(regionalOrder);
            if (!string.IsNullOrEmpty(validationMessage))
            {
                Response.StatusCode = 400;
                return Json(validationMessage, JsonRequestBehavior.AllowGet);
            }

            regionalOrder.UploadedFile = null;
            db.RegionalOrders.Add(regionalOrder);
            try
            {
                db.SaveChanges();
            }
            catch (DbEntityValidationException ex)
            {
                Response.StatusCode = 400;
                return Json(GetEntityValidationMessage(ex), JsonRequestBehavior.AllowGet);
            }

            var storedFile = SaveRegionalOrderFile(file, regionalOrder.RegionalOrderID);
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.RegionalOrder SET FilePath = @p0, OriginalFileName = @p1 WHERE RegionalOrderID = @p2",
                storedFile.RelativePath,
                storedFile.OriginalFileName,
                regionalOrder.RegionalOrderID);

            return Json("Successfully saved the leave application record!", JsonRequestBehavior.AllowGet);
        }

        string ValidateRegionalOrder(RegionalOrder regionalOrder)
        {
            if (regionalOrder == null)
            {
                return "Unable to read the regional order details.";
            }

            if (regionalOrder.RegionalOrderDetails == null || !regionalOrder.RegionalOrderDetails.Any())
            {
                return "Please add at least one DILG personnel.";
            }

            if (regionalOrder.RegionalOrderNumber != null && regionalOrder.RegionalOrderNumber.Length > 10)
            {
                return "Regional Order Number must not exceed 10 characters.";
            }

            if (regionalOrder.Title != null && regionalOrder.Title.Length > 350)
            {
                return "Title must not exceed 350 characters.";
            }

            foreach (var detail in regionalOrder.RegionalOrderDetails)
            {
                if (detail.Venue != null && detail.Venue.Length > 150)
                {
                    return "Venue must not exceed 150 characters.";
                }

                if (detail.Remarks != null && detail.Remarks.Length > 250)
                {
                    return "Personnel remarks must not exceed 250 characters.";
                }
            }

            return null;
        }

        string GetEntityValidationMessage(DbEntityValidationException exception)
        {
            var messages = exception.EntityValidationErrors
                .SelectMany(entityError => entityError.ValidationErrors)
                .Select(error => error.PropertyName + ": " + error.ErrorMessage)
                .Distinct()
                .ToList();

            if (!messages.Any())
            {
                return "Validation failed while saving the regional order.";
            }

            return "Validation failed: " + string.Join("; ", messages);
        }

        public ActionResult GetRegionalOrders()
        {
            if (!CurrentUserCanManageRegionalOrders())
            {
                return new HttpStatusCodeResult(403);
            }

            return GetRegionalOrdersJson(null);
        }

        public ActionResult GetMyRegionalOrders()
        {
            var employeeID = GetCurrentEmployeeID();
            if (!employeeID.HasValue)
            {
                return new HttpStatusCodeResult(401);
            }

            return GetRegionalOrdersJson(employeeID.Value);
        }

        public ActionResult GetMyRegionalOrderCalendarEvents()
        {
            var employeeID = GetCurrentEmployeeID();
            if (!employeeID.HasValue)
            {
                return new HttpStatusCodeResult(401);
            }

            var regionalOrderDates = db.RegionalOrderDetailDates
                .Where(a => a.ActivityDate.HasValue &&
                            a.RegionalOrderDetail.EmployeeID == employeeID.Value)
                .Select(a => new
                {
                    a.ActivityDate,
                    a.RegionalOrderDetail.RegionalOrder.RegionalOrderID,
                    a.RegionalOrderDetail.RegionalOrder.RegionalOrderNumber,
                    a.RegionalOrderDetail.RegionalOrder.Title
                })
                .ToList();

            var events = regionalOrderDates
                .Select(item =>
                {
                    var title = string.Join(" - ", new[] { item.RegionalOrderNumber, item.Title }
                        .Where(value => !string.IsNullOrWhiteSpace(value))).Trim();

                    return new
                    {
                        title = string.IsNullOrWhiteSpace(title) ? "Regional Order" : title,
                        start = item.ActivityDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        allDay = true,
                        url = Url.Action("DisplayFile", "RegionalOrder", new { id = item.RegionalOrderID }),
                        backgroundColor = "#007bff",
                        borderColor = "#007bff"
                    };
                })
                .ToList();

            return Json(events, JsonRequestBehavior.AllowGet);
        }

        ActionResult GetRegionalOrdersJson(short? employeeID)
        {
            // DataTables parameters
            var draw = Request.Form["draw"];
            var start = int.Parse(Request.Form["start"]);
            var length = int.Parse(Request.Form["length"]);
            var searchValue = Request.Form["search[value]"];

            // Apply search filter if needed
            var filteredData = db.RegionalOrders.AsQueryable();// SELECT * FROM Employees
            if (employeeID.HasValue)
            {
                filteredData = filteredData.Where(a => a.RegionalOrderDetails.Any(d => d.EmployeeID == employeeID.Value));
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                filteredData = filteredData.Where(e => e.RegionalOrderNumber.Contains(searchValue) ||
                                                       e.Title.Contains(searchValue) ||
                                                       e.RegionalOrderDetails.Any(d => d.Venue.Contains(searchValue) ||
                                                                                       d.Remarks.Contains(searchValue) ||
                                                                                       d.Employee.LastName.Contains(searchValue) ||
                                                                                       d.Employee.FirstName.Contains(searchValue)));
            }

            // Apply ordering (required for Skip to work)
            filteredData = filteredData.OrderByDescending(e => e.RegionalOrderID); // Choose your default column here


            // Total records and records after filtering
            var totalRecords = filteredData.Count();

            // Apply paging
            var data = filteredData.Skip(start).Take(length).ToList();

            var totalRecordsFiltered = filteredData.Count();

            object jsonData = data.Select(item =>
            {
                return new
                {
                    ActivityDate = string.Join(", ", item.RegionalOrderDetails
                        .SelectMany(a => a.RegionalOrderDetailDates)
                        .Where(a => a.ActivityDate.HasValue)
                        .Select(a => a.ActivityDate.Value.ToShortDateString())
                        .Distinct()
                        .ToList()),
                    Venue = string.Join(", ", item.RegionalOrderDetails
                        .Where(a => !string.IsNullOrEmpty(a.Venue))
                        .Select(a => a.Venue)
                        .Distinct()
                        .ToList()),
                    RegionalOrderNumber= item.RegionalOrderNumber.ToString(),
                    item.Title,
                    RegionalOrderDate= item.RegionalOrderDate.Value.ToShortDateString(),
                    Remarks = string.Join(", ", item.RegionalOrderDetails
                        .Where(a => !string.IsNullOrEmpty(a.Remarks))
                        .Select(a => a.Remarks)
                        .Distinct()
                        .ToList()),
                    item.RegionalOrderID,
                    DILGPersonnel = string.Join("|", item.RegionalOrderDetails.AsQueryable()
                        .Select(a => a.Employee.LastName + ", " + a.Employee.FirstName + " " + a.Employee.MiddleName)
                        .Distinct()
                        .ToList())
                };
            }).ToList();

            //jsonData = data.Select(item => new
            //{

            //// Specify data rows here 
            //DateApplied = Convert.ToString(item?.DateApplied.Value.ToString("MM/dd/yyyy hh:mm tt", CultureInfo.InvariantCulture)),
            //    LeaveDetail = string.Join(", ", item.LeaveApplicationDetails.Select(a => a.TypeOfLeave.TypeOfLeave1).ToList<string>()),
            //    TotalNumberofDays = item.LeaveApplicationDetails
            //                .SelectMany(ld => ld.LeaveApplicationDetailInclusiveDates) // Flatten all inclusive dates
            //                .Count(),  // Count the total number of inclusive dates
            //    Status = item.LeaveApplicationSubmissions.LastOrDefault()?.Status 
            //}).ToList();

            return Json(new
            {
                draw = draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecordsFiltered,
                data = jsonData
            });
        }

        short? GetCurrentEmployeeID()
        {
            if (Session["EmployeeID"] == null)
            {
                return null;
            }

            return Convert.ToInt16(Session["EmployeeID"]);
        }

        bool CurrentUserCanManageRegionalOrders()
        {
            if (Session["Status"] == null)
            {
                return false;
            }

            return Convert.ToInt32(Session["Status"]) != 0;
        }

        bool CurrentUserCanViewRegionalOrder(int regionalOrderID)
        {
            if (CurrentUserCanManageRegionalOrders())
            {
                return true;
            }

            var employeeID = GetCurrentEmployeeID();
            if (!employeeID.HasValue)
            {
                return false;
            }

            return db.RegionalOrderDetails.Any(a =>
                a.RegionalOrderID == regionalOrderID &&
                a.EmployeeID == employeeID.Value);
        }

        RegionalOrderStoredFile SaveRegionalOrderFile(HttpPostedFileBase file, int regionalOrderID)
        {
            var uploadDirectory = Server.MapPath("~/App_Data/RegionalOrders");
            Directory.CreateDirectory(uploadDirectory);

            var originalFileName = Path.GetFileName(file.FileName);
            var storedFileName = regionalOrderID + "_" + Guid.NewGuid().ToString("N") + ".pdf";
            var physicalPath = Path.Combine(uploadDirectory, storedFileName);
            file.SaveAs(physicalPath);

            return new RegionalOrderStoredFile
            {
                OriginalFileName = originalFileName,
                RelativePath = "~/App_Data/RegionalOrders/" + storedFileName
            };
        }

        // Action to display the binary file
        public ActionResult DisplayFile(int id)
        {
            if (!CurrentUserCanViewRegionalOrder(id))
            {
                return new HttpStatusCodeResult(403);
            }

            var storedFile = db.Database.SqlQuery<RegionalOrderStoredFile>(
                "SELECT FilePath AS RelativePath, OriginalFileName FROM dbo.RegionalOrder WHERE RegionalOrderID = @p0",
                id).FirstOrDefault();

            if (storedFile != null && !string.IsNullOrWhiteSpace(storedFile.RelativePath))
            {
                var physicalPath = Server.MapPath(storedFile.RelativePath);
                if (System.IO.File.Exists(physicalPath))
                {
                    Response.AppendHeader("Content-Disposition", "inline; filename=\"" + storedFile.OriginalFileName + "\"");
                    return File(physicalPath, "application/pdf");
                }
            }

            var binaryFile = db.RegionalOrders.Where(a => a.RegionalOrderID == id).FirstOrDefault();
            if (binaryFile != null && binaryFile.UploadedFile != null)
            {
                Response.AppendHeader("Content-Disposition", "inline");
                return File(binaryFile.UploadedFile, "application/pdf");
            }
            return HttpNotFound();
        }

        class RegionalOrderStoredFile
        {
            public string RelativePath { get; set; }
            public string OriginalFileName { get; set; }
        }
    }
}
