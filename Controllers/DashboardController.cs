using Microsoft.AspNetCore.Mvc;
using UniManage.Data;
using System.Linq;
using System;

namespace UniManage.Controllers
{
    public class DashboardController : Controller
    {
        private readonly UniManageDbContext _context;

        public DashboardController(UniManageDbContext context)
        {
            _context = context;
        }

        // Submissions over time (last 6 months) for lecturer view
        [HttpGet]
        public IActionResult LecturerSubmissionsOverTime()
        {
            var months = Enumerable.Range(0, 6)
                                   .Select(i => DateTime.UtcNow.AddMonths(-(5 - i)))
                                   .ToList();

            var labels = months.Select(m => m.ToString("MMM yyyy")).ToArray();
            var data = months.Select(m => _context.Submissions.Count(s => s.SubmissionDate.Year == m.Year && s.SubmissionDate.Month == m.Month)).ToArray();

            return Json(new { labels, data });
        }

        // Student: submissions over time (last 6 months)
        [HttpGet]
        public IActionResult StudentSubmissionsOverTime()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return Json(new { labels = new string[0], data = new int[0] });

            var months = Enumerable.Range(0, 6)
                                   .Select(i => DateTime.UtcNow.AddMonths(-(5 - i)))
                                   .ToList();

            var labels = months.Select(m => m.ToString("MMM yyyy")).ToArray();
            var data = months.Select(m => _context.Submissions.Count(s => s.UserId == user.UserId && s.SubmissionDate.Year == m.Year && s.SubmissionDate.Month == m.Month)).ToArray();

            return Json(new { labels, data });
        }

        // Student: graded submissions per enrolled course (count of graded submissions)
        [HttpGet]
        public IActionResult StudentGradesByCourse()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return Json(new { labels = new string[0], data = new int[0] });

            var courseIds = _context.Enrollments.Where(e => e.UserId == user.UserId).Select(e => e.CourseId).ToList();

            var grouped = courseIds.Select(cid => new {
                CourseId = cid,
                CourseName = _context.Courses.Where(c => c.CourseId == cid).Select(c => c.CourseName).FirstOrDefault() ?? ("Course " + cid),
                GradedCount = _context.Submissions.Count(s => s.UserId == user.UserId && _context.Assignments.Where(a => a.AssignmentId == s.AssignmentId).Select(a => a.CourseId).FirstOrDefault() == cid && !string.IsNullOrEmpty(s.Grade))
            }).ToList();

            var labels = grouped.Select(g => g.CourseName).ToArray();
            var data = grouped.Select(g => g.GradedCount).ToArray();

            return Json(new { labels, data });
        }

        // Submissions grouped by assignment (top 6)
        [HttpGet]
        public IActionResult LecturerSubmissionsByAssignment()
        {
            var grouped = _context.Submissions
                .GroupBy(s => s.AssignmentId)
                .Select(g => new
                {
                    AssignmentId = g.Key,
                    Count = g.Count(),
                    Title = _context.Assignments.Where(a => a.AssignmentId == g.Key).Select(a => a.Title).FirstOrDefault()
                })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToList();

            var labels = grouped.Select(x => x.Title ?? $"Assignment {x.AssignmentId}").ToArray();
            var counts = grouped.Select(x => x.Count).ToArray();

            return Json(new { labels, counts });
        }

        // Grade status distribution for lecturer (graded vs ungraded)
        [HttpGet]
        public IActionResult LecturerGradeStatus()
        {
            var graded = _context.Submissions.Count(s => s.Grade != null && s.Grade != "");
            var ungraded = _context.Submissions.Count() - graded;

            var labels = new[] { "Graded", "Ungraded" };
            var data = new[] { graded, ungraded };

            return Json(new { labels, data });
        }

        public IActionResult StudentDashboard()
        {
            try
            {
                var userEmail = HttpContext.Session.GetString("UserEmail");
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                if (user == null) return RedirectToAction("Index", "Home");

                // safe guards: ensure DbSets are available
                var enrollments = (_context.Enrollments as IQueryable<Models.Enrollment>) ?? Enumerable.Empty<Models.Enrollment>().AsQueryable();
                var coursesSet = (_context.Courses as IQueryable<Models.Course>) ?? Enumerable.Empty<Models.Course>().AsQueryable();
                var assignmentsSet = (_context.Assignments as IQueryable<Models.Assignment>) ?? Enumerable.Empty<Models.Assignment>().AsQueryable();
                var submissionsSet = (_context.Submissions as IQueryable<Models.Submission>) ?? Enumerable.Empty<Models.Submission>().AsQueryable();

                // Enrolled courses
                var courseIds = enrollments.Where(e => e.UserId == user.UserId).Select(e => e.CourseId).ToList();
                var courses = coursesSet.Where(c => courseIds.Contains(c.CourseId)).ToList();

                // Upcoming assignments for enrolled courses
                var upcoming = assignmentsSet
                    .Where(a => courseIds.Contains(a.CourseId) && a.Deadline >= DateTime.UtcNow)
                    .OrderBy(a => a.Deadline)
                    .Take(10)
                    .ToList();

                // Submissions (grades & feedback) for this student
                var subs = submissionsSet
                    .Where(s => s.UserId == user.UserId)
                    .OrderByDescending(s => s.SubmissionDate)
                    .Select(s => new Models.SubmissionSummary {
                        SubmissionId = s.SubmissionId,
                        AssignmentId = s.AssignmentId,
                        AssignmentTitle = assignmentsSet.Where(a => a.AssignmentId == s.AssignmentId).Select(a => a.Title).FirstOrDefault(),
                        StudentId = s.UserId,
                        StudentName = user.FullName,
                        CourseId = assignmentsSet.Where(a => a.AssignmentId == s.AssignmentId).Select(a => a.CourseId).FirstOrDefault(),
                        CourseName = coursesSet.Where(c => c.CourseId == assignmentsSet.Where(a => a.AssignmentId == s.AssignmentId).Select(a => a.CourseId).FirstOrDefault()).Select(c => c.CourseName).FirstOrDefault(),
                        SubmissionDate = s.SubmissionDate,
                        Grade = s.Grade,
                        Feedback = s.Feedback
                    })
                    .ToList();

                var vm = new Models.StudentDashboardViewModel {
                    EnrolledCourses = courses,
                    UpcomingAssignments = upcoming,
                    Submissions = subs
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                // fail gracefully and show an empty dashboard with an error message
                TempData["Error"] = "Could not load dashboard. " + ex.Message;
                var vm = new Models.StudentDashboardViewModel();
                return View(vm);
            }
        }

        // Student: graded vs ungraded counts for current student
        [HttpGet]
        public IActionResult StudentGradeStatus()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return Json(new { labels = new string[0], data = new int[0] });

            var graded = _context.Submissions.Count(s => s.UserId == user.UserId && !string.IsNullOrEmpty(s.Grade));
            var ungraded = _context.Submissions.Count(s => s.UserId == user.UserId) - graded;

            var labels = new[] { "Graded", "Ungraded" };
            var data = new[] { graded, ungraded };

            return Json(new { labels, data });
        }

        // Student: upcoming assignments (titles and days until deadline)
        [HttpGet]
        public IActionResult StudentUpcomingAssignments()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return Json(new { labels = new string[0], data = new int[0] });

            var courseIds = _context.Enrollments.Where(e => e.UserId == user.UserId).Select(e => e.CourseId).ToList();

            var upcoming = _context.Assignments
                .Where(a => courseIds.Contains(a.CourseId) && a.Deadline >= DateTime.UtcNow)
                .OrderBy(a => a.Deadline)
                .Take(10)
                .ToList()
                .Select(a => new {
                    title = a.Title,
                    days = (int)Math.Ceiling((a.Deadline - DateTime.UtcNow).TotalDays)
                })
                .ToList();

            var labels = upcoming.Select(x => x.title).ToArray();
            var data = upcoming.Select(x => x.days).ToArray();

            return Json(new { labels, data });
        }

        public IActionResult LecturerDashboard()
        {
            try
            {
                // Determine current lecturer from session
                var userEmail = HttpContext.Session.GetString("UserEmail");
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

                // Courses that have assignments (workaround for missing course-lecturer mapping)
                var courses = _context.Courses
                    .Where(c => _context.Assignments.Any(a => a.CourseId == c.CourseId))
                    .ToList();

                var assignments = _context.Assignments
                    .OrderBy(a => a.Deadline)
                    .ToList();

                // Recent submissions (limit 10)
                var recent = _context.Submissions
                    .OrderByDescending(s => s.SubmissionDate)
                    .Take(10)
                    .Select(s => new Models.SubmissionSummary {
                        SubmissionId = s.SubmissionId,
                        AssignmentId = s.AssignmentId,
                        AssignmentTitle = _context.Assignments.Where(a => a.AssignmentId == s.AssignmentId).Select(a => a.Title).FirstOrDefault(),
                        StudentId = s.UserId,
                        StudentName = _context.Users.Where(u => u.UserId == s.UserId).Select(u => u.FullName).FirstOrDefault(),
                        CourseId = _context.Assignments.Where(a => a.AssignmentId == s.AssignmentId).Select(a => a.CourseId).FirstOrDefault(),
                        CourseName = _context.Courses.Where(c => c.CourseId == _context.Assignments.Where(a => a.AssignmentId == s.AssignmentId).Select(a => a.CourseId).FirstOrDefault()).Select(c => c.CourseName).FirstOrDefault(),
                        SubmissionDate = s.SubmissionDate,
                        Grade = s.Grade,
                        Feedback = s.Feedback
                    })
                    .ToList();

                // Grading summary per assignment
                var grading = _context.Assignments
                    .Select(a => new Models.GradingSummary {
                        AssignmentId = a.AssignmentId,
                        AssignmentTitle = a.Title,
                        TotalSubmissions = _context.Submissions.Count(s => s.AssignmentId == a.AssignmentId),
                        GradedCount = _context.Submissions.Count(s => s.AssignmentId == a.AssignmentId && s.Grade != null),
                        UngradedCount = _context.Submissions.Count(s => s.AssignmentId == a.AssignmentId && s.Grade == null)
                    })
                    .OrderByDescending(g => g.TotalSubmissions)
                    .ToList();

                var vm = new Models.LecturerDashboardViewModel {
                    Courses = courses,
                    Assignments = assignments,
                    RecentSubmissions = recent,
                    GradingSummaries = grading
                };

                return View(vm);
            }
            catch (Exception ex)
            {
                // fail gracefully and show an empty dashboard with an error message
                TempData["Error"] = "Could not load dashboard. " + ex.Message;
                var vm = new Models.LecturerDashboardViewModel();
                return View(vm);
            }
        }

        public IActionResult AdminDashboard()
        {
            return View();
        }

        [HttpGet]
        public IActionResult SummaryTotals()
        {
            var totals = new
            {
                Users = _context.Users.Count(),
                Courses = _context.Courses.Count(),
                Enrollments = _context.Enrollments.Count(),
                Assignments = _context.Assignments.Count(),
                Messages = _context.Messages.Count(),
                Students = _context.Users.Count(u => u.Role == "Student"),
                Lecturers = _context.Users.Count(u => u.Role == "Lecturer")
            };

            return Json(totals);
        }

        [HttpGet]
        public IActionResult RoleDistribution()
        {
            var student = _context.Users.Count(u => u.Role == "Student");
            var lecturer = _context.Users.Count(u => u.Role == "Lecturer");
            var admin = _context.Users.Count(u => u.Role == "Admin");
            var others = _context.Users.Count() - (student + lecturer + admin);

            var labels = new[] { "Student", "Lecturer", "Admin", "Other" };
            var data = new[] { student, lecturer, admin, others };

            return Json(new { labels, data });
        }

        [HttpGet]
        public IActionResult EnrollmentsOverTime()
        {
            var months = Enumerable.Range(0, 6)
                                   .Select(i => DateTime.UtcNow.AddMonths(-(5 - i)))
                                   .ToList();

            var labels = months.Select(m => m.ToString("MMM yyyy")).ToArray();
            var data = months
                .Select(m => _context.Enrollments.Count(e => e.EnrollmentDate.Year == m.Year && e.EnrollmentDate.Month == m.Month))
                .ToArray();

            return Json(new { labels, data });
        }

        [HttpGet]
        public IActionResult TopCoursesByEnrollments()
        {
            var grouped = _context.Enrollments
                .GroupBy(e => e.CourseId)
                .Select(g => new
                {
                    CourseId = g.Key,
                    Count = g.Count(),
                    CourseName = _context.Courses.Where(c => c.CourseId == g.Key).Select(c => c.CourseName).FirstOrDefault()
                })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToList();

            var labels = grouped.Select(x => x.CourseName ?? $"Course {x.CourseId}").ToArray();
            var counts = grouped.Select(x => x.Count).ToArray();

            return Json(new { labels, counts });
        }
    }
}
