using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ImageMetadataParser.Data
{
    /// <summary>
    /// Entity Framework database context for the Image Metadata Parser application
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Image metadata records
        /// </summary>
        public DbSet<ImageMetadata> ImageMetadataRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ImageMetadata entity
            modelBuilder.Entity<ImageMetadata>(entity =>
            {
                // Table name
                entity.ToTable("ImageMetadata");

                // Primary key
                entity.HasKey(e => e.Id);

                // Configure indexes for common queries
                entity.HasIndex(e => e.FileName)
                      .HasDatabaseName("IX_ImageMetadata_FileName");

                entity.HasIndex(e => e.FileHash)
                      .HasDatabaseName("IX_ImageMetadata_FileHash")
                      .IsUnique()
                      .HasFilter("[FileHash] IS NOT NULL");

                entity.HasIndex(e => e.CreatedAt)
                      .HasDatabaseName("IX_ImageMetadata_CreatedAt");

                entity.HasIndex(e => e.DateTaken)
                      .HasDatabaseName("IX_ImageMetadata_DateTaken")
                      .HasFilter("[DateTaken] IS NOT NULL");

                entity.HasIndex(e => e.ProcessingStatus)
                      .HasDatabaseName("IX_ImageMetadata_ProcessingStatus");

                entity.HasIndex(e => e.UserId)
                      .HasDatabaseName("IX_ImageMetadata_UserId")
                      .HasFilter("[UserId] IS NOT NULL");

                entity.HasIndex(e => e.BatchId)
                      .HasDatabaseName("IX_ImageMetadata_BatchId")
                      .HasFilter("[BatchId] IS NOT NULL");

                // Composite index for GPS coordinates
                entity.HasIndex(e => new { e.GpsLatitude, e.GpsLongitude })
                      .HasDatabaseName("IX_ImageMetadata_GPS")
                      .HasFilter("[GpsLatitude] IS NOT NULL AND [GpsLongitude] IS NOT NULL");

                // Configure string lengths and types
                entity.Property(e => e.FileName)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(e => e.MimeType)
                      .HasMaxLength(50);

                entity.Property(e => e.FileHash)
                      .HasMaxLength(32);

                entity.Property(e => e.CameraMake)
                      .HasMaxLength(100);

                entity.Property(e => e.CameraModel)
                      .HasMaxLength(100);

                entity.Property(e => e.LensInfo)
                      .HasMaxLength(200);

                entity.Property(e => e.LocationName)
                      .HasMaxLength(200);

                entity.Property(e => e.Copyright)
                      .HasMaxLength(500);

                entity.Property(e => e.Artist)
                      .HasMaxLength(200);

                entity.Property(e => e.Software)
                      .HasMaxLength(200);

                entity.Property(e => e.ColorSpace)
                      .HasMaxLength(50);

                entity.Property(e => e.WhiteBalance)
                      .HasMaxLength(50);

                entity.Property(e => e.Flash)
                      .HasMaxLength(100);

                entity.Property(e => e.MeteringMode)
                      .HasMaxLength(50);

                entity.Property(e => e.ExposureMode)
                      .HasMaxLength(50);

                entity.Property(e => e.SceneCaptureType)
                      .HasMaxLength(50);

                entity.Property(e => e.ProcessingStatus)
                      .IsRequired()
                      .HasMaxLength(20)
                      .HasDefaultValue("Pending");

                entity.Property(e => e.UserId)
                      .HasMaxLength(100);

                entity.Property(e => e.BatchId)
                      .HasMaxLength(50);

                // Configure text columns
                entity.Property(e => e.ExifData)
                      .HasColumnType("text");

                entity.Property(e => e.XmpData)
                      .HasColumnType("text");

                entity.Property(e => e.IptcData)
                      .HasColumnType("text");

                entity.Property(e => e.AiAnalysis)
                      .HasColumnType("text");

                entity.Property(e => e.Keywords)
                      .HasColumnType("text");

                entity.Property(e => e.Description)
                      .HasColumnType("text");

                entity.Property(e => e.ErrorMessage)
                      .HasColumnType("text");

                // Configure decimal precision for camera settings
                entity.Property(e => e.Aperture)
                      .HasPrecision(4, 2);

                entity.Property(e => e.ShutterSpeed)
                      .HasPrecision(10, 6);

                entity.Property(e => e.FocalLength)
                      .HasPrecision(6, 2);

                // Configure GPS coordinates with high precision
                entity.Property(e => e.GpsLatitude)
                      .HasPrecision(10, 7);

                entity.Property(e => e.GpsLongitude)
                      .HasPrecision(10, 7);

                entity.Property(e => e.GpsAltitude)
                      .HasPrecision(8, 2);

                // Configure timestamps
                entity.Property(e => e.CreatedAt)
                      .IsRequired()
                      .HasDefaultValueSql("GETUTCDATE()");

                entity.Property(e => e.UpdatedAt)
                      .IsRequired()
                      .HasDefaultValueSql("GETUTCDATE()");

                // Ignore computed properties
                entity.Ignore(e => e.FileSize);
                entity.Ignore(e => e.Dimensions);
                entity.Ignore(e => e.IsProcessed);
                entity.Ignore(e => e.HasErrors);
                entity.Ignore(e => e.CameraSettings);
                entity.Ignore(e => e.GpsCoordinates);
                entity.Ignore(e => e.KeywordList);
                entity.Ignore(e => e.OriginalFilePath);
            });

            // Add any additional entity configurations here
        }

        /// <summary>
        /// Override SaveChanges to automatically update timestamps
        /// </summary>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        /// <summary>
        /// Override SaveChangesAsync to automatically update timestamps
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Updates the UpdatedAt timestamp for modified entities
        /// </summary>
        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<ImageMetadata>()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Seed initial data if needed
        /// </summary>
        public void SeedData()
        {
            // Add any initial data seeding logic here
            // This method can be called during application startup
        }

        /// <summary>
        /// Get metadata records with optional filtering and pagination
        /// </summary>
        public async Task<List<ImageMetadata>> GetImageMetadataAsync(
            string? userId = null,
            string? batchId = null,
            string? processingStatus = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            var query = ImageMetadataRecords.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(x => x.UserId == userId);

            if (!string.IsNullOrEmpty(batchId))
                query = query.Where(x => x.BatchId == batchId);

            if (!string.IsNullOrEmpty(processingStatus))
                query = query.Where(x => x.ProcessingStatus == processingStatus);

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Search metadata records by keywords or filename
        /// </summary>
        public async Task<List<ImageMetadata>> SearchImageMetadataAsync(
            string searchTerm,
            string? userId = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            var query = ImageMetadataRecords.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(x => x.UserId == userId);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(x =>
                    x.FileName.Contains(searchTerm) ||
                    (x.Keywords != null && x.Keywords.Contains(searchTerm)) ||
                    (x.Description != null && x.Description.Contains(searchTerm)) ||
                    (x.AiAnalysis != null && x.AiAnalysis.Contains(searchTerm)) ||
                    (x.LocationName != null && x.LocationName.Contains(searchTerm)) ||
                    (x.Artist != null && x.Artist.Contains(searchTerm))
                );
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Get statistics about the metadata collection
        /// </summary>
        public async Task<object> GetMetadataStatsAsync(string? userId = null, CancellationToken cancellationToken = default)
        {
            var query = ImageMetadataRecords.AsQueryable();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(x => x.UserId == userId);

            var stats = await query
                .GroupBy(x => 1)
                .Select(g => new
                {
                    TotalImages = g.Count(),
                    TotalSizeBytes = g.Sum(x => x.FileSizeBytes),
                    ProcessedImages = g.Count(x => x.ProcessingStatus == "Completed"),
                    FailedImages = g.Count(x => x.ProcessingStatus == "Failed"),
                    PendingImages = g.Count(x => x.ProcessingStatus == "Pending"),
                    ImagesWithGps = g.Count(x => x.GpsLatitude != null && x.GpsLongitude != null),
                    ImagesWithAiAnalysis = g.Count(x => x.AiAnalysis != null),
                    OldestImage = g.Min(x => x.DateTaken),
                    NewestImage = g.Max(x => x.DateTaken),
                    TopCameraMake = g.GroupBy(x => x.CameraMake)
                                     .Where(cg => cg.Key != null)
                                     .OrderByDescending(cg => cg.Count())
                                     .Select(cg => cg.Key)
                                     .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            return stats ?? new
            {
                TotalImages = 0,
                TotalSizeBytes = 0L,
                ProcessedImages = 0,
                FailedImages = 0,
                PendingImages = 0,
                ImagesWithGps = 0,
                ImagesWithAiAnalysis = 0,
                OldestImage = (DateTime?)null,
                NewestImage = (DateTime?)null,
                TopCameraMake = (string?)null
            };
        }
    }
}