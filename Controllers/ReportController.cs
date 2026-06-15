using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using UniManage.Data;
using System.Linq;
using System.Text;
using System.Globalization;

namespace UniManage.Controllers
{
    public class ReportController : Controller
    {
        private readonly UniManageDbContext _context;

        public ReportController(UniManageDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewBag.TotalUsers = _context.Users.Count();
            ViewBag.TotalCourses = _context.Courses.Count();
            ViewBag.TotalEnrollments = _context.Enrollments.Count();
            ViewBag.TotalAssignments = _context.Assignments.Count();
            ViewBag.TotalMessages = _context.Messages.Count();
            ViewBag.TotalStudents = _context.Users.Count(u => u.Role == "Student");
            ViewBag.TotalLecturers = _context.Users.Count(u => u.Role == "Lecturer");

            return View();
        }

        [HttpGet]
        public IActionResult DownloadUsersCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("UserId,FullName,Email,Role");
            var users = _context.Users.Select(u => new { u.UserId, u.FullName, u.Email, u.Role }).ToList();
            foreach (var u in users)
            {
                sb.AppendLine($"{u.UserId},\"{u.FullName}\",\"{u.Email}\",{u.Role}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "users.csv");
        }

        [HttpGet]
        public IActionResult DownloadCoursesCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("CourseId,CourseCode,CourseName,Credits,Department");
            var courses = _context.Courses.Select(c => new { c.CourseId, c.CourseCode, c.CourseName, c.Credits, c.Department }).ToList();
            foreach (var c in courses)
            {
                sb.AppendLine($"{c.CourseId},\"{c.CourseCode}\",\"{c.CourseName}\",{c.Credits},\"{c.Department}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "courses.csv");
        }

        [HttpGet]
        public IActionResult DownloadEnrollmentsCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("EnrollmentId,UserId,UserEmail,CourseId,CourseName,EnrollmentDate");

            var items = _context.Enrollments
                .Select(e => new
                {
                    e.EnrollmentId,
                    e.UserId,
                    UserEmail = _context.Users.Where(u => u.UserId == e.UserId).Select(u => u.Email).FirstOrDefault(),
                    e.CourseId,
                    CourseName = _context.Courses.Where(c => c.CourseId == e.CourseId).Select(c => c.CourseName).FirstOrDefault(),
                    e.EnrollmentDate
                }).ToList();

            foreach (var e in items)
            {
                var date = e.EnrollmentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                sb.AppendLine($"{e.EnrollmentId},{e.UserId},\"{e.UserEmail}\",{e.CourseId},\"{e.CourseName}\",{date}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "enrollments.csv");
        }

        [HttpGet]
        public IActionResult EnrollmentsOverTime(DateTime? start, DateTime? end)
        {
            // If no range provided, return last 6 months
            var months = new List<DateTime>();
            if (!start.HasValue || !end.HasValue)
            {
                months = Enumerable.Range(0, 6).Select(i => DateTime.UtcNow.AddMonths(-(5 - i))).ToList();
            }
            else
            {
                var s = new DateTime(start.Value.Year, start.Value.Month, 1);
                var e = new DateTime(end.Value.Year, end.Value.Month, 1);
                for (var m = s; m <= e; m = m.AddMonths(1)) months.Add(m);
            }

            var labels = months.Select(m => m.ToString("MMM yyyy")).ToArray();
            var data = months.Select(m => _context.Enrollments.Count(e => e.EnrollmentDate.Year == m.Year && e.EnrollmentDate.Month == m.Month)).ToArray();

            return Json(new { labels, data });
        }

        [HttpGet]
        public IActionResult LecturerWorkload()
        {
            // Build a simple workload summary per lecturer. Since courses are not assigned to lecturers in the schema,
            // we use uploaded course materials and received messages as workload indicators.
            var lecturers = _context.Users.Where(u => u.Role == "Lecturer").ToList();

            var model = lecturers.Select(l => new UniManage.Models.LecturerWorkloadViewModel
            {
                UserId = l.UserId,
                FullName = l.FullName,
                CoursesCount = 0, // no direct mapping in current schema
                MaterialsUploaded = _context.CourseMaterials != null ? _context.CourseMaterials.Count(m => m.UploadedBy == l.UserId) : 0,
                MessagesReceived = _context.Messages != null ? _context.Messages.Count(m => m.ReceiverId == l.UserId) : 0
            }).ToList();

            return View(model);
        }

        [HttpGet]
        public IActionResult LecturerWorkloadData()
        {
            try
            {
                var lecturers = _context.Users.Where(u => u.Role == "Lecturer").ToList();

                var model = lecturers.Select(l =>
                {
                    var materials = 0;
                    var messages = 0;
                    try { materials = _context.CourseMaterials != null ? _context.CourseMaterials.Count(m => m.UploadedBy == l.UserId) : 0; } catch { materials = 0; }
                    try { messages = _context.Messages != null ? _context.Messages.Count(m => m.ReceiverId == l.UserId) : 0; } catch { messages = 0; }

                    return new
                    {
                        UserId = l.UserId,
                        FullName = l.FullName,
                        CoursesCount = 0,
                        MaterialsUploaded = materials,
                        MessagesReceived = messages
                    };
                }).ToList();

                return Json(model);
            }
            catch (System.Exception ex)
            {
                // return error information for debugging
                return Json(new { error = ex.Message, details = ex.ToString() });
            }
        }
    }
}
