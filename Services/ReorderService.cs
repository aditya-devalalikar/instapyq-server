using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using pqy_server.Models.Generic;
using pqy_server.Models.Order;

namespace pqy_server.Services
{
    public class ReorderService
    {
        private readonly AppDbContext _context;

        public ReorderService(AppDbContext context)
        {
            _context = context;
        }

        public async Task ReorderAsync<T>(
            IQueryable<T> scopeQuery,
            T entity,
            int newOrder
        ) where T : class, IOrderable
        {
            int oldOrder = entity.Order;

            if (newOrder <= 0)
                throw new ArgumentException("Order must be greater than zero.");

            if (oldOrder == newOrder)
                return;

            var items = await scopeQuery
                .OrderBy(x => x.Order)
                .ToListAsync();

            int maxOrder = items.Count;
            if (newOrder > maxOrder)
                newOrder = maxOrder;

            // Step 1: remove entity from sequence
            entity.Order = -1;
            await _context.SaveChangesAsync();

            foreach (var item in items)
            {
                if (item.Id == entity.Id) continue;

                if (newOrder > oldOrder &&
                    item.Order > oldOrder &&
                    item.Order <= newOrder)
                {
                    item.Order--;
                }

                if (newOrder < oldOrder &&
                    item.Order >= newOrder &&
                    item.Order < oldOrder)
                {
                    item.Order++;
                }
            }

            // Step 2: set final order
            entity.Order = newOrder;
            await _context.SaveChangesAsync();
        }
    }
}
