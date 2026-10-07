using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.DomainEventsDispatching;
using Microsoft.EntityFrameworkCore;

namespace CompanyName.MyMeetings.BuildingBlocks.Infrastructure
{
    // Commits a business operation: dispatches the domain events, then saves all changes in a single database transaction.
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbContext _context;
        private readonly IDomainEventsDispatcher _domainEventsDispatcher;

        // Receives the EF Core context and the domain events dispatcher.
        public UnitOfWork(
            DbContext context,
            IDomainEventsDispatcher domainEventsDispatcher)
        {
            this._context = context;
            this._domainEventsDispatcher = domainEventsDispatcher;
        }

        // Dispatches the domain events (adding outbox messages) and then calls SaveChanges, so data and outbox rows are saved together.
        public async Task<int> CommitAsync(
            CancellationToken cancellationToken = default,
            Guid? internalCommandId = null)
        {
            await this._domainEventsDispatcher.DispatchEventsAsync();

            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}