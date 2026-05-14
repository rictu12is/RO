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

            var regionalOrders = db.RegionalOrders
                .Where(a => a.RegionalOrderDetails.Any(d => d.EmployeeID == employeeID.Value))
                .ToList();

            var events = new List<object>();
            var today = DateTime.Today;

            foreach (var regionalOrder in regionalOrders)
            {
                var activityDates = regionalOrder.RegionalOrderDetails
                    .SelectMany(detail => detail.RegionalOrderDetailDates)
                    .Where(date => date.ActivityDate.HasValue)
                    .Select(date => date.ActivityDate.Value.Date)
                    .Distinct()
                    .OrderBy(date => date)
                    .ToList();

                if (!activityDates.Any())
                {
                    continue;
                }

                var eventTitle = string.Join(" - ", new[] { regionalOrder.RegionalOrderNumber, regionalOrder.Title }
                    .Where(value => !string.IsNullOrWhiteSpace(value))).Trim();

                if (string.IsNullOrWhiteSpace(eventTitle))
                {
                    eventTitle = "Regional Order";
                }

                var details = regionalOrder.RegionalOrderDetails.ToList();
                var participants = details
                    .Where(detail => detail.Employee != null)
                    .Select(detail => detail.Employee.LastName + ", " + detail.Employee.FirstName + " " + detail.Employee.MiddleName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .ToList();

                var venues = details
                    .Where(detail => !string.IsNullOrWhiteSpace(detail.Venue))
                    .Select(detail => detail.Venue)
                    .Distinct()
                    .ToList();

                var remarks = details
                    .Where(detail => !string.IsNullOrWhiteSpace(detail.Remarks))
                    .Select(detail => detail.Remarks)
                    .Distinct()
                    .ToList();

                var eventDetails = new
                {
                    regionalOrderID = regionalOrder.RegionalOrderID,
                    regionalOrderNumber = regionalOrder.RegionalOrderNumber ?? string.Empty,
                    title = regionalOrder.Title ?? string.Empty,
                    regionalOrderDate = regionalOrder.RegionalOrderDate.HasValue
                        ? regionalOrder.RegionalOrderDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                        : string.Empty,
                    venue = string.Join(", ", venues),
                    activityDates = activityDates
                        .Select(date => date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture))
                        .ToList(),
                    participants = participants,
                    remarks = string.Join(", ", remarks),
                    pdfUrl = Url.Action("DisplayFile", "RegionalOrder", new { id = regionalOrder.RegionalOrderID })
                };

                var rangeStart = activityDates.First();
                var previousDate = rangeStart;

                for (var index = 1; index <= activityDates.Count; index++)
                {
                    var startsNewRange = index == activityDates.Count ||
                        activityDates[index] != previousDate.AddDays(1);

                    if (startsNewRange)
                    {
                        var statusCategory = previousDate < today
                            ? "past"
                            : rangeStart > today
                                ? "upcoming"
                                : "today";
                        var statusColor = statusCategory == "past"
                            ? "#28a745"
                            : statusCategory == "today"
                                ? "#ffc107"
                                : "#007bff";

                        events.Add(new
                        {
                            title = eventTitle,
                            start = rangeStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            end = previousDate.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            allDay = true,
                            backgroundColor = statusColor,
                            borderColor = statusColor,
                            textColor = statusCategory == "today" ? "#1f2d3d" : "#ffffff",
                            extendedProps = new
                            {
                                eventDetails.regionalOrderID,
                                eventDetails.regionalOrderNumber,
                                eventDetails.title,
                                eventDetails.regionalOrderDate,
                                eventDetails.venue,
                                eventDetails.activityDates,
                                eventDetails.participants,
                                eventDetails.remarks,
                                eventDetails.pdfUrl,
                                statusCategory = statusCategory,
                                statusColor = statusColor,
                                rangeLabel = rangeStart == previousDate
                                    ? rangeStart.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                                    : rangeStart.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) + " - " +
                                      previousDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)
                            }
                        });

                        if (index < activityDates.Count)
                        {
                            rangeStart = activityDates[index];
                        }
                    }

                    if (index < activityDates.Count)
                    {
                        previousDate = activityDates[index];
                    }
                }
            }

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
