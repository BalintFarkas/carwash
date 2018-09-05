﻿using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;

namespace MSHU.CarWash.ClassLibrary.Models
{
    public class ApplicationDbContext : IdentityDbContext<User>, IPushDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Reservation> Reservation { get; set; }
        public DbSet<PushSubscription> PushSubscription { get; set; }

        /** 
         * WORKAROUND:
         * DbContext.Update() sometimes throws an InvalidOperationException: The instance of entity type 'X' cannot be tracked because another instance of this type with the same key is already being tracked.
         * Normally I wasn't able to reproduce the bug, but if you stop the code at a breakpoint before Update() and expand the Results View of the _context.X object the issue will turn up.
         * In this case, it is understandable, as you've enumerated the list and loaded the objects, so there will be two when you try to update, therefore the exception.
         * But the exception has been thrown in deployed production environment, where it shouldn't.
         * This solves the issue.
         * 
         * For more info: https://stackoverflow.com/questions/48117961/
         **/
        public override EntityEntry<TEntity> Update<TEntity>(TEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            try
            {
                // Try first with normal Update
                return base.Update(entity);
            }
            catch (InvalidOperationException)
            {
                // Load original object from database
                var originalEntity = Find(entity.GetType(), ((IEntity)entity).Id);

                // Set the updated values
                Entry(originalEntity).CurrentValues.SetValues(entity);

                // Return the expected return object of Update()
                return Entry((TEntity)originalEntity);
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            builder.Entity<PushSubscription>()
                .HasIndex(s => s.Id)
                .IsUnique();
        }

        /**
         * Implement this interface in all DB model classes!
         **/
        public interface IEntity
        {
            /// <summary>
            /// Id of the given object.
            /// </summary>
            string Id { get; }
        }

        /*
         * Db migration:
         * 
         * 1. Tools –> NuGet Package Manager –> Package Manager Console
         * 
         * 2. Add-Migration MigrationName
         * 
         * 3. Update-Database
         * 
         * 
         * Generating SQL script:
         * 
         *    Script-Migration -From NotIncludedMigrationName -To IncludedMigrationName
         */
    }
}
