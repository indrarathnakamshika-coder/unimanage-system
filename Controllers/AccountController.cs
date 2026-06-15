using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UniManage.Data;
using UniManage.Models;
using System.Linq;

namespace UniManage.Controllers
{
    public class AccountController : Controller
    {
        private readonly UniManageDbContext _context;

        public AccountController(UniManageDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(User user)
        {
            if (ModelState.IsValid)
            {
                _context.Users.Add(user);
                _context.SaveChanges();
                return RedirectToAction("Login");
            }
            return View(user);
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email && u.Password == password);

            if (user != null)
            {
                HttpContext.Session.SetString("UserRole", user.Role);
                HttpContext.Session.SetString("UserName", user.FullName);
                HttpContext.Session.SetString("UserEmail", user.Email);

                if (user.Role == "Student")
                    return RedirectToAction("StudentDashboard", "Dashboard");

                if (user.Role == "Lecturer")
                    return RedirectToAction("LecturerDashboard", "Dashboard");

                if (user.Role == "Admin")
                    return RedirectToAction("AdminDashboard", "Dashboard");
            }

            ViewBag.Error = "Invalid email or password";
            return View();
        }
    }
}
