using Microsoft.EntityFrameworkCore;
using pqy_server.Models;
using pqy_server.Models.Activity;
using pqy_server.Models.Bookmark;
using pqy_server.Models.Content;
using pqy_server.Models.Exams;
using pqy_server.Models.Images;
using pqy_server.Models.Logs;
using pqy_server.Models.Notifications;
using pqy_server.Models.Orders;
using pqy_server.Models.Progress;
using pqy_server.Models.QuestionNotes;
using pqy_server.Models.Questions;
using pqy_server.Models.Quotes;
using pqy_server.Models.Roles;
using pqy_server.Models.Subjects;
using pqy_server.Models.Topics;
using pqy_server.Models.Users;
using pqy_server.Models.Years;
using pqy_server.Models.Mains;
using pqy_server.Models.Otp;
using pqy_server.Models.Streak;

namespace pqy_server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        // 📦 Tables
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Year> Years { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Topic> Topics { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }
        public DbSet<BookmarkQuestion> Bookmarks { get; set; }
        public DbSet<QuestionReport> QuestionReports { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<QuestionNote> QuestionNotes { get; set; }
        public DbSet<EnumLabel> EnumLabels { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<UserDailyProgress> UserDailyProgress { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<ExamProgress> ExamProgress { get; set; }
        public DbSet<ImageFile> Images { get; set; }
        public DbSet<QuestionAnswerImage> QuestionAnswerImages { get; set; }
        public DbSet<MainsQuestion> MainsQuestions { get; set; }
        public DbSet<EmailOtp> EmailOtps { get; set; }
        public DbSet<ContentPage> ContentPages { get; set; }

        // 📦 Streak & Study Timer tables
        public DbSet<pqy_server.Models.Streak.Streak> Streaks { get; set; }
        public DbSet<pqy_server.Models.Streak.StreakMonthlyProgress> StreakMonthlyProgress { get; set; }
        public DbSet<pqy_server.Models.Streak.DailyStudySummary> DailyStudySummary { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Configuration is always provided by DI (Program.cs); no fallback needed.
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Roles: seed & unique
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "User" }
            );
            modelBuilder.Entity<Role>()
                .HasIndex(r => r.RoleName)
                .IsUnique();

            // ✅ User → Role FK
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId);

            // ✅ SelectedExamIds (string list)
            modelBuilder.Entity<User>()
                .Property(u => u.SelectedExamIds)
                .HasConversion(
                    v => string.Join(",", v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                );

            // ✅ PostgreSQL timestamps → use NOW()
            modelBuilder.Entity<User>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<Question>()
                .Property(q => q.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // ✅ Store enums as strings
            modelBuilder.Entity<Question>()
                .Property(q => q.DifficultyLevel)
                .HasConversion<string>();

            modelBuilder.Entity<Question>()
                .Property(q => q.Nature)
                .HasConversion<string>();

            modelBuilder.Entity<Question>()
                .Property(q => q.SourceType)
                .HasConversion<string>();

            // ✅ FK for Question
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Exam)
                .WithMany()
                .HasForeignKey(q => q.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Subject)
                .WithMany()
                .HasForeignKey(q => q.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Topic)
                .WithMany()
                .HasForeignKey(q => q.TopicId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Year)
                .WithMany()
                .HasForeignKey(q => q.YearId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ Order → User FK
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany() // or .WithMany(u => u.Orders) if you add a collection in User
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ✅ Default values
            modelBuilder.Entity<Order>()
                .Property(o => o.CreatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<Order>()
                .Property(o => o.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // ✅ Store OrderStatus enum as string for backward compatibility
            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion<string>();

            // ✅ Index on PaymentId for refund lookups via payment_id
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.PaymentId);

            // ✅ Soft delete filters
            modelBuilder.Entity<Question>()
                .HasQueryFilter(q => !q.IsDeleted);

            modelBuilder.Entity<User>()
                .HasQueryFilter(u => !u.IsDeleted);

            // ✅ Indexes
            modelBuilder.Entity<Question>().HasIndex(q => q.ExamId);
            modelBuilder.Entity<Question>().HasIndex(q => q.SubjectId);
            modelBuilder.Entity<Question>().HasIndex(q => q.TopicId);
            modelBuilder.Entity<Question>().HasIndex(q => q.YearId);
            modelBuilder.Entity<Question>().HasIndex(q => q.IsDeleted);
            modelBuilder.Entity<Question>()
                .HasIndex(q => new { q.ExamId, q.SubjectId, q.TopicId, q.YearId, q.QuestionId });
            modelBuilder.Entity<Question>()
                .HasIndex(q => new { q.YearId, q.QuestionId });

            modelBuilder.Entity<UserActivity>().HasIndex(a => a.UserId);
            modelBuilder.Entity<UserActivity>().HasIndex(a => a.QuestionId);
            modelBuilder.Entity<UserActivity>().HasIndex(a => a.ActivityTime);
            modelBuilder.Entity<UserActivity>().HasIndex(a => new { a.UserId, a.ActivityTime });

            modelBuilder.Entity<BookmarkQuestion>().HasIndex(b => new { b.UserId, b.QuestionId });

            modelBuilder.Entity<QuestionReport>().HasIndex(r => r.QuestionId);
            modelBuilder.Entity<QuestionReport>().HasIndex(r => r.UserId);
            modelBuilder.Entity<QuestionReport>().HasIndex(r => r.IsResolved);

            modelBuilder.Entity<Notification>().HasIndex(n => n.UserId);
            modelBuilder.Entity<Notification>().HasIndex(n => n.IsRead);

            modelBuilder.Entity<User>().HasIndex(u => u.Username);
            modelBuilder.Entity<User>().HasIndex(u => u.UserEmail).IsUnique();

            modelBuilder.Entity<Order>().HasIndex(o => o.RazorpayOrderId).IsUnique();
            modelBuilder.Entity<Order>().HasIndex(o => new { o.UserId, o.Status, o.ExpiresAt });
            modelBuilder.Entity<Order>().HasIndex(o => new { o.UserId, o.CreatedAt });

            modelBuilder.Entity<EnumLabel>().HasIndex(e => new { e.EnumType, e.EnumName });

            modelBuilder.Entity<Topic>()
                .HasIndex(t => new { t.SubjectId, t.TopicOrder })
                .IsUnique();
            modelBuilder.Entity<ImageFile>()
                .Property(i => i.ImageType)
                .HasConversion<string>();

            modelBuilder.Entity<QuestionAnswerImage>()
                .HasOne(qi => qi.Image)
                .WithMany()
                .HasForeignKey(qi => qi.ImageId)
                .OnDelete(DeleteBehavior.Cascade);

            // QuestionId is the primary filter in GetQuestionImagesByQuestionIdAsync
            // which is called on every question list and exam fetch
            modelBuilder.Entity<QuestionAnswerImage>()
                .HasIndex(qi => qi.QuestionId);

            // ✅ EmailOtp indexes
            modelBuilder.Entity<EmailOtp>().HasIndex(o => o.Email);
            modelBuilder.Entity<EmailOtp>().HasIndex(o => o.ExpiresAt);
            modelBuilder.Entity<EmailOtp>().HasIndex(o => new { o.Email, o.CreatedAt });

            // ✅ Content pages (FAQs, About, Privacy, Terms)
            modelBuilder.Entity<ContentPage>().HasIndex(c => c.Slug).IsUnique();
            modelBuilder.Entity<ContentPage>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("NOW()");
            modelBuilder.Entity<ContentPage>()
                .Property(c => c.UpdatedAt)
                .HasDefaultValueSql("NOW()");
            modelBuilder.Entity<ContentPage>()
                .Property(c => c.IsPublished)
                .HasDefaultValue(true);

            // ✅ UserDailyProgress — composite PK + leaderboard indexes
            modelBuilder.Entity<UserDailyProgress>()
                .HasKey(p => new { p.UserId, p.Date, p.SubjectId, p.ExamId });

            // UserId — used by every leaderboard GROUP BY
            modelBuilder.Entity<UserDailyProgress>()
                .HasIndex(p => p.UserId);

            // Date — used by today/week/month/year period filters
            modelBuilder.Entity<UserDailyProgress>()
                .HasIndex(p => p.Date);

            // (UserId, Date) — used by streak & consistency queries
            modelBuilder.Entity<UserDailyProgress>()
                .HasIndex(p => new { p.UserId, p.Date });

            // ExamProgress leaderboard indexes
            // (UserId, CompletedAt) — exam count & accuracy boards
            modelBuilder.Entity<ExamProgress>()
                .HasIndex(ep => new { ep.UserId, ep.CompletedAt });

            // (ModeType, CompletedAt) — exam-type filtered boards
            modelBuilder.Entity<ExamProgress>()
                .HasIndex(ep => new { ep.ModeType, ep.CompletedAt });

            // ─── Streaks ──────────────────────────────────────────────────────────────

            // Soft delete filter
            modelBuilder.Entity<pqy_server.Models.Streak.Streak>()
                .HasQueryFilter(s => !s.IsDeleted);

            // FK: Streak → User
            modelBuilder.Entity<pqy_server.Models.Streak.Streak>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ClientId must be unique per user to prevent duplicate syncs
            modelBuilder.Entity<pqy_server.Models.Streak.Streak>()
                .HasIndex(s => s.ClientId)
                .IsUnique();

            modelBuilder.Entity<pqy_server.Models.Streak.Streak>()
                .HasIndex(s => new { s.UserId, s.IsDeleted });

            modelBuilder.Entity<pqy_server.Models.Streak.Streak>()
                .Property(s => s.CreatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<pqy_server.Models.Streak.Streak>()
                .Property(s => s.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // ─── StreakMonthlyProgress ────────────────────────────────────────────────

            // Composite PK: (StreakId, YearMonth)
            modelBuilder.Entity<StreakMonthlyProgress>()
                .HasKey(p => new { p.StreakId, p.YearMonth });

            modelBuilder.Entity<StreakMonthlyProgress>()
                .HasOne(p => p.Streak)
                .WithMany()
                .HasForeignKey(p => p.StreakId)
                .OnDelete(DeleteBehavior.Cascade);

            // Denormalized UserId index for fast user-scoped date range queries
            modelBuilder.Entity<StreakMonthlyProgress>()
                .HasIndex(p => new { p.UserId, p.YearMonth });

            // ─── DailyStudySummary ────────────────────────────────────────────────────

            // Composite PK: (UserId, Date)
            modelBuilder.Entity<DailyStudySummary>()
                .HasKey(s => new { s.UserId, s.Date });

            modelBuilder.Entity<DailyStudySummary>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DailyStudySummary>()
                .HasIndex(s => new { s.UserId, s.Date });

            // ─────────────────────────────────────────────────────────────────────────

            modelBuilder.Entity<ContentPage>().HasData(
                new ContentPage
                {
                    Id = 1,
                    Slug = "faqs",
                    Title = "FAQs",
                    ContentJson = "[]",
                    IsPublished = true,
                    CreatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc)
                },
                new ContentPage
                {
                    Id = 2,
                    Slug = "privacy-policy",
                    Title = "Privacy Policy",
                    ContentHtml = string.Empty,
                    IsPublished = true,
                    CreatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc)
                },
                new ContentPage
                {
                    Id = 3,
                    Slug = "terms-conditions",
                    Title = "Terms & Conditions",
                    ContentHtml = string.Empty,
                    IsPublished = true,
                    CreatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc)
                },
                new ContentPage
                {
                    Id = 4,
                    Slug = "about-us",
                    Title = "About Us",
                    ContentHtml = string.Empty,
                    IsPublished = true,
                    CreatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc),
                    UpdatedAt = new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }
    }
}



