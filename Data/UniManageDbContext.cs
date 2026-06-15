using Microsoft.EntityFrameworkCore;
using UniManage.Models;

namespace UniManage.Data
{
    public class UniManageDbContext : DbContext
    {
        public UniManageDbContext(DbContextOptions<UniManageDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UniManage.Models.CourseMaterial> CourseMaterials { get; set; }
        public DbSet<UniManage.Models.AssignmentMaterial> AssignmentMaterials { get; set; }
    }
}