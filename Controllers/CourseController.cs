using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using UniManage.Data;
using UniManage.Models;
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;

namespace UniManage.Controllers
{
    public class CourseController : Controller
    {
        private readonly UniManageDbContext _context;

        public CourseController(UniManageDbContext context)
        {
            _context = context;
        }

        // Details page for a course - includes enrollment availability info
        public IActionResult Details(int id)
        {
            var course = _context.Courses.FirstOrDefault(c => c.CourseId == id);
            if (course == null) return RedirectToAction("Index");

            bool isFull = false;
            if (course.EnrollmentLimit > 0 && _context.Enrollments != null)
            {
                var current = _context.Enrollments.Count(e => e.CourseId == id);
                isFull = current >= course.EnrollmentLimit;
            }

            ViewBag.IsFull = isFull;
            // indicate whether current user is already enrolled in this course
            try
            {
                var userEmail = HttpContext.Session.GetString("UserEmail");
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                bool isEnrolled = false;
                if (user != null && _context.Enrollments != null)
                {
                    isEnrolled = _context.Enrollments.Any(e => e.UserId == user.UserId && e.CourseId == id);
                }
                ViewBag.IsEnrolled = isEnrolled;
            }
            catch { ViewBag.IsEnrolled = false; }
            return View(course);
        }

        public IActionResult Index()
        {
            try
            {
                var coursesSet = (_context.Courses as IQueryable<Course>) ?? Enumerable.Empty<Course>().AsQueryable();
                var courses = coursesSet.ToList();

                // compute which courses are full according to EnrollmentLimit
                var fullSet = new System.Collections.Generic.HashSet<int>();
                if (_context.Enrollments != null)
                {
                    var counts = _context.Enrollments.GroupBy(e => e.CourseId)
                        .Select(g => new { CourseId = g.Key, Count = g.Count() })
                        .ToDictionary(x => x.CourseId, x => x.Count);

                    foreach (var c in courses)
                    {
                        if (c.EnrollmentLimit > 0 && counts.TryGetValue(c.CourseId, out var cur) && cur >= c.EnrollmentLimit)
                        {
                            fullSet.Add(c.CourseId);
                        }
                    }
                }

                ViewBag.FullCourses = fullSet;
                // For students show which courses they are already enrolled in so UI can reflect "Enrolled"
                try
                {
                    var userEmail = HttpContext.Session.GetString("UserEmail");
                    var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
                    var enrolledSet = new System.Collections.Generic.HashSet<int>();
                    if (user != null && _context.Enrollments != null)
                    {
                        enrolledSet = _context.Enrollments.Where(e => e.UserId == user.UserId).Select(e => e.CourseId).ToHashSet();
                    }
                    ViewBag.EnrolledCourses = enrolledSet;
                }
                catch { ViewBag.EnrolledCourses = new System.Collections.Generic.HashSet<int>(); }
                return View(courses);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Could not load courses. " + ex.Message;
                return View(new List<Course>());
            }
        }

        // Lecturer: manage materials for a specific course
        [HttpGet]
        public IActionResult Materials(int id)
        {
            // handle invalid id (e.g. 0) by redirecting to course list
            if (id <= 0)
            {
                TempData["Warning"] = "No course selected.";
                return RedirectToAction("Index");
            }

            try
            {
                var course = _context.Courses.FirstOrDefault(c => c.CourseId == id);
                if (course == null)
                {
                    TempData["Warning"] = "Course not found.";
                    return RedirectToAction("Index");
                }

                List<CourseMaterial> materials = new List<CourseMaterial>();
                if (_context.CourseMaterials != null)
                {
                    materials = _context.CourseMaterials.Where(m => m.CourseId == id).OrderByDescending(m => m.UploadedAt).ToList();
                }
                else
                {
                    // In case the DbSet is not available (migrations not applied), return empty list and show warning
                    TempData["Warning"] = "Course materials are not available. Ensure database migrations have been applied.";
                }

                ViewBag.Course = course;
                return View(materials);
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging (not added here)
                TempData["Error"] = "An error occurred while loading course materials.";
                return View(new List<CourseMaterial>());
            }
        }

        [HttpPost]
        public IActionResult UploadMaterial(int courseId, IFormFile file, string description)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return RedirectToAction("Login", "Account");

            if (file != null && file.Length > 0)
            {
                var fileName = Path.GetFileName(file.FileName);
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/course-materials");
                if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
                var unique = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + fileName;
                var path = Path.Combine(uploads, unique);
                using (var stream = new FileStream(path, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                var material = new Models.CourseMaterial
                {
                    CourseId = courseId,
                    FileName = fileName,
                    FilePath = "/uploads/course-materials/" + unique,
                    UploadedBy = user.UserId,
                    Description = description
                };

                try
                {
                    if (_context.CourseMaterials != null)
                    {
                        _context.CourseMaterials.Add(material);
                        _context.SaveChanges();
                    }
                    else
                    {
                        TempData["Warning"] = "Course materials are not available in the database. Ensure migrations have been applied.";
                    }
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                {
                    // handle missing table or other DB update issues gracefully
                    TempData["Error"] = "Could not save uploaded material. Database error: " + (dbEx.InnerException?.Message ?? dbEx.Message);
                }
                catch (System.Exception ex)
                {
                    TempData["Error"] = "Could not save uploaded material: " + ex.Message;
                }
            }

            return RedirectToAction("Materials", new { id = courseId });
        }

        public IActionResult DownloadMaterial(int id)
        {
            var mat = _context.CourseMaterials.FirstOrDefault(m => m.CourseMaterialId == id);
            if (mat == null) return NotFound();

            var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", mat.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(full)) return NotFound();

            var bytes = System.IO.File.ReadAllBytes(full);
            return File(bytes, "application/octet-stream", mat.FileName);
        }

        [HttpGet]
        public IActionResult Create()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index");
            }
            // populate lecturer list for dropdown (match roles that contain 'lecturer', case-insensitive)
            var usersSet = _context.Users ?? Enumerable.Empty<Models.User>().AsQueryable();
            var lects = usersSet
                .Where(u => !string.IsNullOrEmpty(u.Role) && u.Role.Trim().ToLower().Contains("lecturer"))
                .Select(u => new { Value = u.UserId, Text = (u.FullName ?? u.Email) + " (" + u.Email + ")" })
                .ToList();
            // if no lecturers found, fall back to any users so admin can still assign
            if (!lects.Any())
            {
                lects = usersSet
                    .Select(u => new { Value = u.UserId, Text = (u.FullName ?? u.Email) + " (" + u.Email + ")" })
                    .ToList();
            }
            ViewBag.Lecturers = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(lects, "Value", "Text");
            return View();
        }

        [HttpPost]
        public IActionResult Create(Course course)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index");
            }
            if (ModelState.IsValid)
            {
                _context.Courses.Add(course);
                try
                {
                    _context.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                {
                    TempData["Error"] = "Could not save course. Database error: " + (dbEx.InnerException?.Message ?? dbEx.Message);
                }
                catch (System.Exception ex)
                {
                    TempData["Error"] = "Could not save course: " + ex.Message;
                }
            }
            return View(course);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index");
            }
            var course = _context.Courses.FirstOrDefault(c => c.CourseId == id);
            return View(course);
        }

        [HttpPost]
        public IActionResult Edit(Course course)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index");
            }

            _context.Courses.Update(course);
            _context.SaveChanges();
            return RedirectToAction("Index");
        }

        public IActionResult Delete(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Index");
            }

            var course = _context.Courses.FirstOrDefault(c => c.CourseId == id);
            if (course != null)
            {
                try
                {
                    // remove course materials (files + records)
                    var mats = _context.CourseMaterials?.Where(m => m.CourseId == id).ToList() ?? new System.Collections.Generic.List<CourseMaterial>();
                    foreach (var m in mats)
                    {
                        try
                        {
                            var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", m.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                        }
                        catch { }
                        _context.CourseMaterials.Remove(m);
                    }

                    // remove assignments and their related materials and submissions
                    var assignList = _context.Assignments?.Where(a => a.CourseId == id).ToList() ?? new System.Collections.Generic.List<Assignment>();
                    foreach (var a in assignList)
                    {
                        // remove assignment materials
                        var ams = _context.AssignmentMaterials?.Where(am => am.AssignmentId == a.AssignmentId).ToList() ?? new System.Collections.Generic.List<Models.AssignmentMaterial>();
                        foreach (var am in ams)
                        {
                            try
                            {
                                var full = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", am.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                            }
                            catch { }
                            _context.AssignmentMaterials.Remove(am);
                        }

                        // remove submissions (and their files)
                        var subs = _context.Submissions?.Where(s => s.AssignmentId == a.AssignmentId).ToList() ?? new System.Collections.Generic.List<Submission>();
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
                        _context.Assignments.Remove(a);
                    }

                    // remove the course
                    _context.Courses.Remove(course);
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Could not delete course and related data: " + ex.Message;
                }
            }
            return RedirectToAction("Index");
        }

        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("UserRole") == "Admin";
        }
    }
}
