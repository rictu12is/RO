using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System.IO;
using System.Net.Mime;
using System.Web.Mvc;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;

namespace OnlineLeaveApplication.Controllers
{
    public class ReportController : Controller
    {
        string ServerName = ConfigurationManager.AppSettings["ServerName"];
        string DatabaseName = ConfigurationManager.AppSettings["DatabaseName"];
        string Username = ConfigurationManager.AppSettings["Username"];
        string Password = ConfigurationManager.AppSettings["Password"];

        // GET: Report
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult GenerateReport(int id = 0, string reportType = "")
        {
            ReportDocument rd = new ReportDocument();
            switch (reportType)
            {
                case "LeaveApplicationForm":
                    if (id <= 0)
                    {
                        return new HttpStatusCodeResult(400, "LeaveApplicationID is required.");
                    }

                    EnsureReportDatabaseObjects();
                    rd.Load(Server.MapPath("~/Reports/CRLeaveApplicationForm.rpt"));
                    rd = GetReport(rd);
                    rd.SetParameterValue("LeaveApplicationID", id);
                    ApplyLeaveDetailParameters(rd, id);
                    break;
                default:
                    return new HttpStatusCodeResult(400, "Invalid report type.");
            }

            Stream stream = (Stream)rd.ExportToStream(ExportFormatType.PortableDocFormat);
            stream.Seek(0, SeekOrigin.Begin);

            rd.Dispose();
            rd.Clone();
            var cd = new ContentDisposition
            {
                FileName = reportType + ".pdf",
                Inline = true
            };

            Response.AppendHeader("Content-Disposition", cd.ToString());
            return File(stream, MediaTypeNames.Application.Pdf);
        }

        void ApplyLeaveDetailParameters(ReportDocument rd, int leaveApplicationID)
        {
            using (var db = new OnlineLeaveApplicationEntities())
            {
                var details = db.LeaveApplicationDetails
                    .Where(d => d.LeaveApplicationID == leaveApplicationID)
                    .ToList();
                var leaveTypeNames = db.LeaveApplicationDetails
                    .Where(d => d.LeaveApplicationID == leaveApplicationID)
                    .Select(d => d.TypeOfLeave.TypeOfLeave1)
                    .ToList();

                var sickLeave = details.Where(d => d.TypeOfLeaveID == 3).ToList();

                TrySetLeaveTypeParameter(rd, "pVacationLeaveMark", leaveTypeNames, "Vacation Leave");
                TrySetLeaveTypeParameter(rd, "pMandatoryForcedLeaveMark", leaveTypeNames, "Mandatory/Forced Leave");
                TrySetLeaveTypeParameter(rd, "pSickLeaveMark", leaveTypeNames, "Sick Leave");
                TrySetLeaveTypeParameter(rd, "pMaternityLeaveMark", leaveTypeNames, "Maternity Leave");
                TrySetLeaveTypeParameter(rd, "pPaternityLeaveMark", leaveTypeNames, "Paternity Leave");
                TrySetLeaveTypeParameter(rd, "pSpecialPrivilegeLeaveMark", leaveTypeNames, "Special Privilege Leave");
                TrySetLeaveTypeParameter(rd, "pSoloParentLeaveMark", leaveTypeNames, "Solo Parent Leave");
                TrySetLeaveTypeParameter(rd, "pStudyLeaveMark", leaveTypeNames, "Study Leave");
                TrySetLeaveTypeParameter(rd, "pVAWCLeaveMark", leaveTypeNames, "10-Day VAWC Leave");
                TrySetLeaveTypeParameter(rd, "pRehabilitationPrivilegeMark", leaveTypeNames, "Rehabilitation Privilege");
                TrySetLeaveTypeParameter(rd, "pSpecialLeaveBenefitsForWomenMark", leaveTypeNames, "Special Leave Benefits for Women");
                TrySetLeaveTypeParameter(rd, "pSpecialEmergencyCalamityLeaveMark", leaveTypeNames, "Special Emergency (Calamity) Leave");
                TrySetLeaveTypeParameter(rd, "pAdoptionLeaveMark", leaveTypeNames, "Adoption Leave");

                TrySetParameter(rd, "pWithinPhilippinesMark", details.Any(d => d.WithinThePhilippines == true) ? "/" : string.Empty);
                TrySetParameter(rd, "pLocation", details.Select(d => d.WithinThePhilippinesAbroadLocation).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty);
                TrySetParameter(rd, "pInHospitalMark", sickLeave.Any(d => d.InHospital == true) ? "/" : string.Empty);
                TrySetParameter(rd, "pOutPatientMark", sickLeave.Any(d => d.InHospital != true && !string.IsNullOrWhiteSpace(d.InHospitalOutPatientIllness)) ? "/" : string.Empty);
                TrySetParameter(rd, "pIllness", sickLeave.Select(d => d.InHospitalOutPatientIllness).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty);
                TrySetParameter(rd, "pSpecialLeaveBenefits", details.Select(d => d.SpecialLeaveBenefits).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty);
                TrySetParameter(rd, "pCompletionMark", details.Any(d => d.CompletionOfMastersDegree == true) ? "/" : string.Empty);
                TrySetParameter(rd, "pBARMark", details.Any(d => d.BARBoardExaminationReview == true) ? "/" : string.Empty);
                TrySetParameter(rd, "pMonetizationMark", details.Any(d => d.MonetizationOfLeaveCredits == true) ? "/" : string.Empty);
                TrySetParameter(rd, "pTerminalMark", details.Any(d => d.TerminalLeave == true) ? "/" : string.Empty);
            }
        }

        void TrySetLeaveTypeParameter(ReportDocument rd, string parameterName, System.Collections.Generic.List<string> leaveTypeNames, string leaveTypeName)
        {
            var selected = leaveTypeNames.Any(name => name == leaveTypeName);
            TrySetParameter(rd, parameterName, selected ? "/" : string.Empty);
        }

        void TrySetParameter(ReportDocument rd, string parameterName, string value)
        {
            foreach (ParameterFieldDefinition parameterField in rd.DataDefinition.ParameterFields)
            {
                if (parameterField.Name == parameterName)
                {
                    rd.SetParameterValue(parameterName, value);
                    return;
                }
            }
        }

        void EnsureReportDatabaseObjects()
        {
            var entityConnectionString = ConfigurationManager.ConnectionStrings["OnlineLeaveApplicationEntities"].ConnectionString;
            var entityBuilder = new EntityConnectionStringBuilder(entityConnectionString);

            using (var connection = new SqlConnection(entityBuilder.ProviderConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
IF OBJECT_ID(N'[dbo].[GetTypeOfLeaveApplication]', N'FN') IS NULL
BEGIN
    EXEC(N'
CREATE FUNCTION [dbo].[GetTypeOfLeaveApplication]
(
    @LeaveApplicationID INT,
    @TypeOfLeave VARCHAR(250)
)
RETURNS VARCHAR(1)
AS
BEGIN
    DECLARE @Result VARCHAR(1);

    SELECT @Result = CASE WHEN EXISTS
    (
        SELECT 1
        FROM [dbo].[LeaveApplicationDetail] lad
        INNER JOIN [dbo].[TypeOfLeave] tol
            ON lad.[TypeOfLeaveID] = tol.[TypeOfLeaveID]
        WHERE lad.[LeaveApplicationID] = @LeaveApplicationID
          AND tol.[TypeOfLeave] = @TypeOfLeave
    )
    THEN ''/'' ELSE '''' END;

    RETURN ISNULL(@Result, '''');
END');
END";

                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        ReportDocument GetReport(ReportDocument rd)
        {
            TableLogOnInfo crtableLogoninfo = new TableLogOnInfo();
            ConnectionInfo crConnectionInfo = GetCrystalConnectionInfo();
            Tables CrTables;

            CrTables = rd.Database.Tables;
            foreach (Table crTb in CrTables)
            {
                crtableLogoninfo = crTb.LogOnInfo;
                crtableLogoninfo.ConnectionInfo = crConnectionInfo;
                crTb.ApplyLogOnInfo(crtableLogoninfo);
            }
            rd.SetDatabaseLogon(crConnectionInfo.UserID, crConnectionInfo.Password, crConnectionInfo.ServerName, crConnectionInfo.DatabaseName);
            foreach (ReportDocument rd1 in rd.Subreports)
            {
                Tables crTbs;
                crTbs = rd1.Database.Tables;
                foreach (Table crTb in crTbs)
                {
                    crtableLogoninfo = crTb.LogOnInfo;
                    crtableLogoninfo.ConnectionInfo = crConnectionInfo;
                    crTb.ApplyLogOnInfo(crtableLogoninfo);
                }
            }
            return rd;
        }

        ConnectionInfo GetCrystalConnectionInfo()
        {
            var entityConnectionString = ConfigurationManager.ConnectionStrings["OnlineLeaveApplicationEntities"].ConnectionString;
            var entityBuilder = new EntityConnectionStringBuilder(entityConnectionString);
            var sqlBuilder = new SqlConnectionStringBuilder(entityBuilder.ProviderConnectionString);

            return new ConnectionInfo
            {
                ServerName = sqlBuilder.DataSource,
                DatabaseName = sqlBuilder.InitialCatalog,
                IntegratedSecurity = sqlBuilder.IntegratedSecurity,
                UserID = sqlBuilder.IntegratedSecurity ? string.Empty : sqlBuilder.UserID,
                Password = sqlBuilder.IntegratedSecurity ? string.Empty : sqlBuilder.Password
            };
        }
    }
}
