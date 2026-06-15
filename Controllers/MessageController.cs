using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Controllers
{
    public class MessageController : Controller
    {
        private readonly UniManageDbContext _context;

        public MessageController(UniManageDbContext context)
        {
            _context = context;
        }

        public IActionResult Inbox()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return RedirectToAction("Login", "Account");

            // get all messages where current user is sender or receiver
            var msgs = _context.Messages
                .Where(m => m.SenderId == user.UserId || m.ReceiverId == user.UserId)
                .ToList();

            var convs = msgs
                .Select(m => new {
                    OtherId = m.SenderId == user.UserId ? m.ReceiverId : m.SenderId,
                    Message = m
                })
                .GroupBy(x => x.OtherId)
                .Select(g => new UniManage.Models.ConversationListItem {
                    OtherUserId = g.Key,
                    OtherUserName = _context.Users.Where(u => u.UserId == g.Key).Select(u => u.FullName).FirstOrDefault() ?? "Unknown",
                    LastMessage = g.OrderByDescending(x => x.Message.SentDate).Select(x => x.Message.Content).FirstOrDefault(),
                    LastDate = g.Max(x => x.Message.SentDate),
                    UnreadCount = _context.Messages.Count(m => m.SenderId == g.Key && m.ReceiverId == user.UserId && m.SentDate > DateTime.UtcNow.AddYears(-10)) // placeholder
                })
                .OrderByDescending(c => c.LastDate)
                .ToList();
            ViewBag.Conversations = convs;
            return View();
        }

        [HttpGet]
        public IActionResult ConversationWith(int otherUserId)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return RedirectToAction("Login", "Account");

            var messages = _context.Messages
                .Where(m => (m.SenderId == user.UserId && m.ReceiverId == otherUserId) || (m.SenderId == otherUserId && m.ReceiverId == user.UserId))
                .OrderBy(m => m.SentDate)
                .ToList();

            var other = _context.Users.FirstOrDefault(u => u.UserId == otherUserId);
            ViewBag.CurrentUserId = user.UserId;
            ViewBag.OtherUserId = otherUserId;
            ViewBag.OtherUserName = other?.FullName ?? "User";

            return View("Conversation", messages);
        }

        // Admin: show list of student-lecturer conversations
        public IActionResult AdminInbox()
        {
            // find messages where one side is Student and the other is Lecturer
            var studentRole = "Student";
            var lecturerRole = "Lecturer";

            var msgs = _context.Messages.ToList();

            var convs = msgs
                .Select(m => new { m, sender = _context.Users.FirstOrDefault(u => u.UserId == m.SenderId), receiver = _context.Users.FirstOrDefault(u => u.UserId == m.ReceiverId) })
                .Where(x => (x.sender != null && x.receiver != null) && ((x.sender.Role == studentRole && x.receiver.Role == lecturerRole) || (x.sender.Role == lecturerRole && x.receiver.Role == studentRole)))
                .Select(x => new {
                    StudentId = x.sender.Role == studentRole ? x.sender.UserId : x.receiver.UserId,
                    StudentName = x.sender.Role == studentRole ? x.sender.FullName : x.receiver.FullName,
                    LecturerId = x.sender.Role == lecturerRole ? x.sender.UserId : x.receiver.UserId,
                    LecturerName = x.sender.Role == lecturerRole ? x.sender.FullName : x.receiver.FullName,
                    x.m.SentDate,
                    x.m.Content
                })
                .GroupBy(x => new { x.StudentId, x.LecturerId })
                .Select(g => new UniManage.Models.ConversationSummary {
                    StudentId = g.Key.StudentId,
                    StudentName = g.Select(x => x.StudentName).FirstOrDefault(),
                    LecturerId = g.Key.LecturerId,
                    LecturerName = g.Select(x => x.LecturerName).FirstOrDefault(),
                    LastMessageDate = g.Max(x => x.SentDate),
                    LastMessageSnippet = g.OrderByDescending(x => x.SentDate).Select(x => x.Content).FirstOrDefault()
                })
                .OrderByDescending(c => c.LastMessageDate)
                .ToList();

            return View(convs);
        }

        // Show conversation between a student and lecturer
        [HttpGet]
        public IActionResult Conversation(int studentId, int lecturerId)
        {
            var messages = _context.Messages
                .Where(m => (m.SenderId == studentId && m.ReceiverId == lecturerId) || (m.SenderId == lecturerId && m.ReceiverId == studentId))
                .OrderBy(m => m.SentDate)
                .ToList();

            var student = _context.Users.FirstOrDefault(u => u.UserId == studentId);
            var lecturer = _context.Users.FirstOrDefault(u => u.UserId == lecturerId);

            ViewBag.StudentName = student?.FullName ?? "Student";
            ViewBag.LecturerName = lecturer?.FullName ?? "Lecturer";

            return View(messages);
        }

        [HttpGet]
        public IActionResult Send()
        {
            ViewBag.Users = _context.Users.ToList();
            return View();
        }

        [HttpPost]
        public IActionResult Send(Message message)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var user = _context.Users.FirstOrDefault(u => u.Email == userEmail);

            message.SenderId = user.UserId;

            if (ModelState.IsValid)
            {
                _context.Messages.Add(message);
                _context.SaveChanges();
                return RedirectToAction("Inbox");
            }

            ViewBag.Users = _context.Users.ToList();
            return View(message);
        }
    }
}