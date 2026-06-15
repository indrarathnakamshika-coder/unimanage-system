using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Controllers
{
    public class EnrollmentController : Controller
    {
        private readonly UniManageDbContext _context;

        public EnrollmentController(UniManageDbContext context)
        {
            _context = context;
        }

        public IActionResult Enroll(int id)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var alreadyEnrolled = _context.Enrollments
                .FirstOrDefault(e => e.UserId == user.UserId && e.CourseId == id);

            if (alreadyEnrolled == null)
            {
                Enrollment enrollment = new Enrollment
                {
                    UserId = user.UserId,
                    CourseId = id
                };

                _context.Enrollments.Add(enrollment);
                _context.SaveChanges();
            }

            return RedirectToAction("MyEnrollments");
        }

        public IActionResult MyEnrollments()
        {
            try
            {
                var userEmail = HttpContext.Session.GetString("UserEmail");
                var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

                if (user == null)
                    return RedirectToAction("Login", "Account");

                var enrollments = (_context.Enrollments as IQueryable<Enrollment>) ?? Enumerable.Empty<Enrollment>().AsQueryable();
                var courses = (_context.Courses as IQueryable<Course>) ?? Enumerable.Empty<Course>().AsQueryable();

                var enrolledCourses = from e in enrollments
                                      join c in courses on e.CourseId equals c.CourseId
                                      where e.UserId == user.UserId
                                      select c;

                return View(enrolledCourses.ToList());
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = "Could not load enrollments. " + ex.Message;
                return View(new System.Collections.Generic.List<Course>());
            }
        }
    }
}