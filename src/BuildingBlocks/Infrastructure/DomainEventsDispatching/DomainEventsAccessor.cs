using CompanyName.MyMeetings.BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompanyName.MyMeetings.BuildingBlocks.Infrastructure.DomainEventsDispatching
{
    // Reads the domain events from the entities tracked by the EF Core context.
    public class DomainEventsAccessor : IDomainEventsAccessor
    {
        private readonly DbContext _dbContext;

        // Receives the EF Core context whose change tracker is inspected.
        public DomainEventsAccessor(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Returns the domain events raised by all tracked entities.
        public IReadOnlyCollection<IDomainEvent> GetAllDomainEvents()
        {
            var domainEntities = this._dbContext.ChangeTracker
                .Entries<Entity>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any()).ToList();

            return domainEntities
                .SelectMany(x => x.Entity.DomainEvents)
                .ToList();
        }

        // Removes the domain events from all tracked entities so they are not dispatched twice.
        public void ClearAllDomainEvents()
        {
            var domainEntities = this._dbContext.ChangeTracker
                .Entries<Entity>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any()).ToList();

            domainEntities
                .ForEach(entity => entity.Entity.ClearDomainEvents());
        }
    }
}