using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using UniManage.Data;
using UniManage.Models;
using System.IO;
using System;

namespace UniManage.Controllers
{
    public class AssignmentController : Controller
    {
        private readonly UniManageDbContext _context;

        public AssignmentController(UniManageDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var assignments = _context.Assignments.ToList();

            // if student, compute which assignments they've already submitted so UI can show status
            try
            {
                var role = HttpContext.Session.GetString("UserRole");
                if (role == "Student")
                {
                    var userEmail = HttpContext.Session.GetString("UserEmail");
                    var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                    if (user != null && _context.Submissions != null)
                    {
                        var submitted = _context.Submissions.Where(s => s.UserId == user.UserId).Select(s => s.AssignmentId).ToHashSet();
                        ViewBag.SubmittedAssignments = submitted;
                    }
                    else
                    {
                        ViewBag.SubmittedAssignments = new System.Collections.Generic.HashSet<int>();
                    }
                }
            }
            catch { ViewBag.SubmittedAssignments = new System.Collections.Generic.HashSet<int>(); }

            return View(assignments);
        }

        [HttpGet]
        public IActionResult Create(int? courseId)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Lecturer" && role != "Admin")
            {
                TempData["Error"] = "Only lecturers or administrators may create assignments.";
                return RedirectToAction("Index");
            }

            // pass course context to the view so the lecturer doesn't need to enter CourseId
            if (courseId.HasValue)
            {
                var course = _context.Courses.FirstOrDefault(c => c.CourseId == courseId.Value);
                ViewBag.CourseId = courseId.Value;
                ViewBag.CourseName = course?.CourseName;
                return View(new Assignment { CourseId = courseId.Value });
            }

            // no course selected: provide course list for dropdown
            ViewBag.Courses = _context.Courses.ToList();
            return View(new Assignment());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Assignment assignment, IFormFile attachment)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "Lecturer" && role != "Admin")
            {
                TempData["Error"] = "Only lecturers or administrators may create assignments.";
                return RedirectToAction("Index");
            }

            // basic server-side validation: CourseId must be provided
            if (assignment.CourseId <= 0)
            {
                ModelState.AddModelError("CourseId", "Please select a course.");
            }

            if (ModelState.IsValid)
            {
                _context.Assignments.Add(assignment);
                _context.SaveChanges();

                // handle attachment
                if (attachment != null && attachment.Length > 0)
                {
                    var fileName = Path.GetFileName(attachment.FileName);
                    var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/assignment-materials");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                    var unique = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + fileName;
                    var path = Path.Combine(uploads, unique);
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        attachment.CopyTo(stream);
                    }

                    var mat = new Models.AssignmentMaterial
                    {
                        AssignmentId = assignment.AssignmentId,
                        FileName = fileName,
                        FilePath = "/uploads/assignment-materials/" + unique,
                        UploadedBy = 0
                    };
                    _context.AssignmentMaterials.Add(mat);
                    _context.SaveChanges();
                }

                TempData["Success"] = "Assignment created.";
                return RedirectToAction("Index");
            }

            // if we got here, something failed - ensure view has required data
            ViewBag.Courses = _context.Courses.ToList();
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
            {
                // Only lecturers may delete assignments
                if (HttpContext.Session.GetString("UserRole") != "Lecturer")
                    return RedirectToAction("Index");

                var assignment = _context.Assignments.FirstOrDefault(a => a.AssignmentId == id);
                if (assignment == null)
                {
                    TempData["Error"] = "Assignment not found.";
                    return RedirectToAction("Index");
                }

                try
                {
                    // remove assignment materials (files + records)
                    var mats = _context.AssignmentMaterials.Where(m => m.AssignmentId == id).ToList();
                    foreach (var m in mats)
                    {
                        try
                        {
                            var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", m.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                        }
                        catch { /* ignore file delete errors */ }
                        _context.AssignmentMaterials.Remove(m);
                    }

                    // remove submissions (and their files)
                    var subs = _context.Submissions.Where(s => s.AssignmentId == id).ToList();
                    foreach (var s in subs)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(s.FilePath))
                            {
                                var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", s.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                            }
                        }
                        catch { }
                        _context.Submissions.Remove(s);
                    }

                    // finally remove the assignment
                    _context.Assignments.Remove(assignment);
                    _context.SaveChanges();

                    TempData["Success"] = "Assignment deleted.";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Could not delete assignment: " + ex.Message;
                }

                return RedirectToAction("Index");
            }

            [HttpGet]
            public IActionResult Submit(int id)
            {
                if (HttpContext.Session.GetString("UserRole") != "Student")
                    return RedirectToAction("Index");

            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            ViewBag.AssignmentId = id;
            if (user != null && _context.Submissions != null)
            {
                var existing = _context.Submissions.FirstOrDefault(s => s.AssignmentId == id && s.UserId == user.UserId);
                if (existing != null)
                {
                    ViewBag.ExistingSubmission = true;
                    ViewBag.SubmissionText = existing.SubmissionText;
                    ViewBag.SubmissionFilePath = existing.FilePath;
                }
                else
                {
                    ViewBag.ExistingSubmission = false;
                }
            }
            else
            {
                ViewBag.ExistingSubmission = false;
            }

            return View();
            }

            [HttpPost]
            public IActionResult Submit(int assignmentId, string submissionText, IFormFile file)
            {
                var userEmail = HttpContext.Session.GetString("UserEmail");
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

                if (user == null)
                    return RedirectToAction("Login", "Account");

                string filePath = null;

                if (file != null && file.Length > 0)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);

                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }

                    filePath = "/uploads/" + fileName;
                }

            // if user already submitted for this assignment, update existing record. Otherwise create new.
            var existing = _context.Submissions.FirstOrDefault(s => s.AssignmentId == assignmentId && s.UserId == user.UserId);
            if (existing != null)
            {
                existing.SubmissionText = submissionText;
                existing.SubmissionDate = DateTime.Now;
                if (!string.IsNullOrEmpty(filePath))
                {
                    // attempt to delete previous file if present
                    try
                    {
                        if (!string.IsNullOrEmpty(existing.FilePath))
                        {
                            var fullOld = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existing.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(fullOld)) System.IO.File.Delete(fullOld);
                        }
                    }
                    catch { }

                    existing.FilePath = filePath;
                }

                _context.SaveChanges();
            }
            else
            {
                Submission submission = new Submission
                {
                    AssignmentId = assignmentId,
                    UserId = user.UserId,
                    SubmissionText = submissionText,
                    FilePath = filePath,
                };

                _context.Submissions.Add(submission);
                _context.SaveChanges();
            }

                return RedirectToAction("MySubmissions");
            } 

        public IActionResult MySubmissions()
        {
            if (HttpContext.Session.GetString("UserRole") != "Student")
                return RedirectToAction("Index");

            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            var submissions = _context.Submissions
                .Where(s => s.UserId == user.UserId)
                .ToList();

            return View(submissions);
        }

        public IActionResult GradeList()
        {
            if (HttpContext.Session.GetString("UserRole") != "Lecturer")
                return RedirectToAction("Index");

            try
            {
                var submissions = _context.Submissions?.ToList() ?? new System.Collections.Generic.List<Submission>();
                return View(submissions);
            }
            catch (System.Exception)
            {
                TempData["Error"] = "Could not load submissions. Please check the database.";
                return View(new System.Collections.Generic.List<Submission>());
            }
        }

        [HttpGet]
        public IActionResult Grade(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Lecturer")
                return RedirectToAction("Index");

            var submission = _context.Submissions.FirstOrDefault(s => s.SubmissionId == id);
            return View(submission);
        }

        [HttpPost]
        public IActionResult Grade(Submission submission)
        {
            if (HttpContext.Session.GetString("UserRole") != "Lecturer")
                return RedirectToAction("Index");

            var existing = _context.Submissions.FirstOrDefault(s => s.SubmissionId == submission.SubmissionId);
            if (existing != null)
            {
                existing.Grade = submission.Grade;
                existing.Feedback = submission.Feedback;
                _context.SaveChanges();
            }

            return RedirectToAction("GradeList");
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var assignment = _context.Assignments.FirstOrDefault(a => a.AssignmentId == id);
            if (assignment == null)
                return NotFound();
            // load assignment materials so the Razor view can render download links
            try
            {
                var mats = _context.AssignmentMaterials.Where(m => m.AssignmentId == id).ToList();
                ViewBag.AssignmentMaterials = mats;
            }
            catch { ViewBag.AssignmentMaterials = new System.Collections.Generic.List<AssignmentMaterial>(); }

            // If a real Razor view exists, use it; otherwise generate a simple HTML fallback so the link works
            try
            {
                var detailsViewPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Views", "Assignment", "Details.cshtml");
                if (System.IO.File.Exists(detailsViewPath))
                {
                    return View(assignment);
                }
            }
            catch { }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!doctype html>");
            sb.AppendLine("<html><head><meta charset=\"utf-8\"><title>" + System.Net.WebUtility.HtmlEncode(assignment.Title) + "</title>");
            sb.AppendLine("<link rel=\"stylesheet\" href=\"/css/site.css\" />");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<div class=\"container\">\n<div class=\"card shadow-sm mt-4\">\n<div class=\"card-body\">\n");
            sb.AppendLine("<h2>" + System.Net.WebUtility.HtmlEncode(assignment.Title) + "</h2>");
            sb.AppendLine("<p>" + System.Net.WebUtility.HtmlEncode(assignment.Description) + "</p>");
            sb.AppendLine("<dl>");
            sb.AppendLine("<dt>Assignment ID</dt><dd>" + assignment.AssignmentId + "</dd>");
            sb.AppendLine("<dt>Course ID</dt><dd>" + assignment.CourseId + "</dd>");
            sb.AppendLine("<dt>Deadline</dt><dd>" + System.Net.WebUtility.HtmlEncode(assignment.Deadline.ToString("F")) + "</dd>");
            sb.AppendLine("</dl>");

            if (_context.AssignmentMaterials != null)
            {
                var mats = _context.AssignmentMaterials.Where(m => m.AssignmentId == id).ToList();
                if (mats.Any())
                {
                    sb.AppendLine("<h4>Materials</h4><ul>");
                    foreach (var m in mats)
                    {
                        sb.AppendLine("<li><a href=\"" + System.Net.WebUtility.HtmlEncode(m.FilePath) + "\">" + System.Net.WebUtility.HtmlEncode(m.FileName) + "</a></li>");
                    }
                    sb.AppendLine("</ul>");
                }
            }

            sb.AppendLine("<p><a href=\"/Assignment\">Back to assignments</a></p>");
            sb.AppendLine("</div></div></div>");
            sb.AppendLine("</body></html>");

            return Content(sb.ToString(), "text/html");
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (HttpContext.Session.GetString("UserRole") != "Lecturer")
                return RedirectToAction("Index");

            var assignment = _context.Assignments.FirstOrDefault(a => a.AssignmentId == id);
            if (assignment == null)
                return NotFound();

            ViewBag.Courses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Courses.ToList(), "CourseId", "CourseName", assignment?.CourseId);
            return View(assignment);
        }

        [HttpPost]
        public IActionResult Edit(Assignment assignment)
        {
            if (HttpContext.Session.GetString("UserRole") != "Lecturer")
                return RedirectToAction("Index");

            if (!ModelState.IsValid)
            {
                ViewBag.Courses = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Courses.ToList(), "CourseId", "CourseName", assignment?.CourseId);
                return View(assignment);
            }

            var existing = _context.Assignments.FirstOrDefault(a => a.AssignmentId == assignment.AssignmentId);
            if (existing == null)
                return NotFound();

            existing.Title = assignment.Title;
            existing.Description = assignment.Description;
            existing.Deadline = assignment.Deadline;
            existing.CourseId = assignment.CourseId;

            _context.SaveChanges();

            return RedirectToAction("Index");
        }
    }
}
