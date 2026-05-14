using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineLeaveApplication.Controllers
{
    public class LeaveApplicationController : Controller
    {
        OnlineLeaveApplicationEntities db = new OnlineLeaveApplicationEntities();
        // GET: LeaveApplication
        public ActionResult SaveLeaveApplication(List<LeaveApplicationDetail> list)
        {
            var serverDate = db.Database.SqlQuery<DateTime>("SELECT GETDATE()").Single();
            LeaveApplication leaveApplication = new LeaveApplication { EmployeeID = 1, DateApplied= serverDate };
            leaveApplication.LeaveApplicationDetails = list;
            // Add all leave applications to the DbSet (this is an efficient way to save the list)
            db.LeaveApplications.Add(leaveApplication);

            var leaveApplicationSubmission = new LeaveApplicationSubmission();
            leaveApplicationSubmission.DateSubmitted = serverDate;
            leaveApplicationSubmission.LeaveApplicationID = leaveApplication.LeaveApplicationID;
            leaveApplicationSubmission.SubmittedBy = 1;
            leaveApplicationSubmission.Status = 1; // Draft 
            db.LeaveApplicationSubmissions.Add(leaveApplicationSubmission);

            // Save all changes to the database in one transaction
            db.SaveChanges();
            return Json("Successfully saved the leave application record!", JsonRequestBehavior.AllowGet);
        }

        public ActionResult SubmitLeaveApplication(int leaveApplicationID, string remarks, bool isReturned = false)
        {
            var employeeID = Convert.ToInt16(Session["EmployeeID"]);

            var la = db.LeaveApplicationSubmissions.Where(a => a.LeaveApplicationID == leaveApplicationID).AsEnumerable().LastOrDefault();
            la.ReceivedBy = employeeID;

            //Status = status == "1" ? "Draft" :
            //         status == "2" ? "For Review" :
            //         status == "3" ? "Reviewed" :
            //         status == "4" ? "For Approval" :
            //         status == "5" ? "Approved" :
            //         status == "0" ? "Returned" : ""
            LeaveApplicationSubmission leaveApplicationSubmission = new LeaveApplicationSubmission();
            var serverDate = db.Database.SqlQuery<DateTime>("SELECT GETDATE()").Single();
            leaveApplicationSubmission.LeaveApplicationID = leaveApplicationID;
            leaveApplicationSubmission.SubmittedBy = employeeID;
            leaveApplicationSubmission.DateSubmitted = serverDate;
            leaveApplicationSubmission.Status = !isReturned ? la.Status == 0 ? (short)(la.Status + 2) : (short)(la.Status + 1) : (short)0;
            leaveApplicationSubmission.Remarks = remarks;
            db.LeaveApplicationSubmissions.Add(leaveApplicationSubmission);
            db.SaveChanges();

            return Json("Successfully submitted leave application record!", JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetLeaveApplications(bool forApproval =false)
        {
            var employeeID = Convert.ToInt16(Session["EmployeeID"]);
            int stats = Convert.ToInt16(Session["Status"]);

            // DataTables parameters
            var draw = Request.Form["draw"];
            var start = int.Parse(Request.Form["start"]);
            var length = int.Parse(Request.Form["length"]);
            var searchValue = Request.Form["search[value]"];

            // Apply search filter if needed
            var filteredData = db.LeaveApplications.Where(a=> a.EmployeeID == employeeID).AsQueryable();// SELECT * FROM Employees
            if (forApproval)
            {
                filteredData = db.LeaveApplications.Where(a => a.LeaveApplicationSubmissions
                           .OrderByDescending(s => s.DateSubmitted)
                           .FirstOrDefault().Status == stats);
            }

            if (!string.IsNullOrEmpty(searchValue))
            {
                //filteredData = filteredData.Where(e => e.Lastname.Contains(searchValue) ||
                //                                                 e.Firstname.Contains(searchValue) ||
                //                                                 e.Middlename.Contains(searchValue));
            }
            // Apply ordering (required for Skip to work)
            filteredData = filteredData.OrderByDescending(e => e.DateApplied); // Choose your default column here


            // Total records and records after filtering
            var totalRecords = filteredData.Count();

            // Apply paging
            var data = filteredData.Skip(start).Take(length).ToList();

            var totalRecordsFiltered = filteredData.Count();
             
            object jsonData = data.Select(item =>
            {
                var status = item.LeaveApplicationSubmissions.LastOrDefault()?.Status; // Safe null handling

                return new
                {
                    item.LeaveApplicationID,
                    DateApplied = item.DateApplied.HasValue
                        ? item.DateApplied.Value.ToString("MM/dd/yyyy hh:mm tt", CultureInfo.InvariantCulture)
                        : string.Empty, // Handling potential null values

                    LeaveDetail = string.Join(", ", item.LeaveApplicationDetails
                        .Select(a => a.TypeOfLeave.TypeOfLeave1)
                        .ToList()),

                    LeaveDates = string.Join("<br>", item.LeaveApplicationDetails
                        .Select(b => "<b>" + b.TypeOfLeave.TypeOfLeave1 + " </b>" + "("+ string.Join( ", ", b.LeaveApplicationDetailInclusiveDates.Select(c=> c.LeaveDate.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)))+")")
                        .ToList()),

                    TotalNumberofDays = item.LeaveApplicationDetails
                        .SelectMany(ld => ld.LeaveApplicationDetailInclusiveDates)
                        .Count(),
                        DILGPersonnel = item.Employee.LastName + ", " + item.Employee.FirstName + " " + item.Employee.MiddleName,
                        Attachments = string.Join(", ", item.LeaveApplicationAttachments1
                        .Select(a => a.LeaveApplicationAttachmentID.ToString() + "|" + a.FileName)
                        .ToList()),
                    Status = status == 1 ? "Draft" :
                    status == 2 ? "For Review" :
                    status == 3 ? "Reviewed" : 
                    status == 4 ? "For Approval" : 
                    status == 5 ? "Approved" : 
                    status == 0 ? "Returned" : ""
                }; 
            }).ToList();

            return Json(new
            {
                draw = draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecordsFiltered,
                data = jsonData
            });
        }
        public ActionResult SaveMainOffice(MainOffice mainOffice) {
            if (mainOffice.MainOfficeID != 0)
            {
                //var obj = db.MainOffices.Where(a => a.MainOfficeID == mainOffice.MainOfficeID).FirstOrDefault();
                //obj = mainOffice;
                db.MainOffices.Attach(mainOffice); 
                db.Entry(mainOffice).State = EntityState.Modified;
            }
            else
            {
                db.MainOffices.Add(mainOffice);
            }
            db.SaveChanges();
            return Json("Successfully submitted leave application record!", JsonRequestBehavior.AllowGet);
        }
        public ActionResult GetSignatories()
        {
            // DataTables parameters
            var draw = Request.Form["draw"];
            var start = int.Parse(Request.Form["start"]);
            var length = int.Parse(Request.Form["length"]);
            var searchValue = Request.Form["search[value]"];

            // Apply search filter if needed
            var filteredData = db.MainOffices.AsQueryable();//  

            if (!string.IsNullOrEmpty(searchValue))
            {
                //filteredData = filteredData.Where(e => e.Lastname.Contains(searchValue) ||
                //                                                 e.Firstname.Contains(searchValue) ||
                //                                                 e.Middlename.Contains(searchValue));
            }
            // Apply ordering (required for Skip to work)
            filteredData = filteredData.OrderByDescending(e => e.MainOfficeID); // Choose your default column here
            // Total records and records after filtering
            var totalRecords = filteredData.Count();

            // Apply paging
            var data = filteredData.Skip(start).Take(length).ToList();

            var totalRecordsFiltered = filteredData.Count();

            object jsonData = data.Select(item =>
            {

                return new
                {
                    item.MainOfficeID,
                    item.MainOfficeName,
                    ForCertification = item.Employee==null?"": item.Employee.LastName + ", " + item.Employee.FirstName,
                    ForReview = item.Employee1 == null ? "" : item.Employee1.LastName + ", " + item.Employee1.FirstName,
                    ForApproval = item.Employee2 == null ? "" : item.Employee2.LastName + ", " + item.Employee2.FirstName,
                    ForCertificationID = item.ForInitialReview,
                    ForReviewID = item.ForReview,
                    ForApprovalID = item.ForApproval,
                };
            }).ToList();

            return Json(new
            {
                draw = draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecordsFiltered,
                data = jsonData
            });
        }

        public ActionResult DisplayFile(int id)
        {
            var binaryFile = db.LeaveApplicationAttachments.Where(a => a.LeaveApplicationAttachmentID == id).FirstOrDefault();
            if (binaryFile != null)
            {
                return File(binaryFile.UploadedFile, "application/pdf");
            }
            return HttpNotFound();
        }
    }
}
